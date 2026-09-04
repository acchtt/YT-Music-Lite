use std::{
    collections::HashMap,
    sync::{
        atomic::{AtomicU16, AtomicU64, Ordering},
        Arc,
    },
};

use axum::{
    body::Body,
    extract::{Path, State},
    http::{header, HeaderMap, Response, StatusCode},
    routing::get,
    Router,
};
use reqwest::{
    header::{ACCEPT_RANGES, CONTENT_LENGTH, CONTENT_RANGE, CONTENT_TYPE, ORIGIN, RANGE, REFERER, USER_AGENT},
    Client,
};
use rustypipe::{
    client::{ClientType, RustyPipe},
    model::AudioStream,
};
use serde::Serialize;
use tokio::sync::RwLock;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedAudioStream {
    /// Loopback-only HTTP URL consumed by WebView2's HTML audio element.
    pub url: String,
    pub mime: String,
    pub bitrate: u32,
    pub duration_seconds: f64,
}

#[derive(Debug, Clone)]
struct RemoteAudioStream {
    video_id: String,
    url: String,
    user_agent: String,
    mime: String,
}

#[derive(Debug)]
pub struct ProxyAudioResponse {
    pub status: u16,
    pub content_type: String,
    pub content_length: Option<String>,
    pub content_range: Option<String>,
    pub accept_ranges: Option<String>,
    pub body: Vec<u8>,
}

#[derive(Clone)]
pub struct PlaybackResolverState {
    client: RustyPipe,
    http: Client,
    streams: Arc<RwLock<HashMap<String, RemoteAudioStream>>>,
    sequence: Arc<AtomicU64>,
    proxy_port: Arc<AtomicU16>,
}

impl Default for PlaybackResolverState {
    fn default() -> Self {
        Self {
            client: RustyPipe::new(),
            http: Client::builder()
                .redirect(reqwest::redirect::Policy::limited(5))
                .build()
                .expect("failed to create playback HTTP client"),
            streams: Arc::new(RwLock::new(HashMap::new())),
            sequence: Arc::new(AtomicU64::new(1)),
            proxy_port: Arc::new(AtomicU16::new(0)),
        }
    }
}

impl PlaybackResolverState {
    /// Start a loopback-only HTTP server used solely for media delivery to WebView2.
    /// Normal HTTP Range handling is substantially more reliable for HTML media than
    /// a mapped custom WebView2 protocol.
    pub async fn start_local_proxy(&self) -> Result<u16, String> {
        if self.proxy_port.load(Ordering::Acquire) != 0 {
            return Ok(self.proxy_port.load(Ordering::Relaxed));
        }

        let listener = tokio::net::TcpListener::bind(("127.0.0.1", 0))
            .await
            .map_err(|e| format!("Could not bind playback proxy: {e}"))?;
        let port = listener
            .local_addr()
            .map_err(|e| format!("Could not read playback proxy address: {e}"))?
            .port();

        self.proxy_port.store(port, Ordering::Release);

        let router = Router::new()
            .route("/stream/{key}", get(proxy_handler))
            .with_state(self.clone());

        tauri::async_runtime::spawn(async move {
            if let Err(error) = axum::serve(listener, router).await {
                eprintln!("YTM playback proxy stopped: {error}");
            }
        });

        Ok(port)
    }

    pub async fn resolve(&self, video_id: &str) -> Result<ResolvedAudioStream, String> {
        if video_id.trim().is_empty() {
            return Err("Track is missing a YouTube video id.".into());
        }

        let port = self.proxy_port.load(Ordering::Acquire);
        if port == 0 {
            return Err("Playback proxy is not ready. Restart YTM Desktop and try again.".into());
        }

        let (remote, bitrate, duration_seconds) = self.resolve_remote(video_id).await?;
        let key = format!(
            "{}-{}",
            video_id.replace(|c: char| !c.is_ascii_alphanumeric() && c != '-' && c != '_', "_"),
            self.sequence.fetch_add(1, Ordering::Relaxed)
        );
        let mime = remote.mime.clone();
        self.streams.write().await.insert(key.clone(), remote);

        Ok(ResolvedAudioStream {
            url: format!("http://127.0.0.1:{port}/stream/{key}"),
            mime,
            bitrate,
            duration_seconds,
        })
    }

    async fn resolve_remote(&self, video_id: &str) -> Result<(RemoteAudioStream, u32, f64), String> {
        let mut failures = Vec::new();

        // Resolve and probe each app client separately. A player response can succeed while
        // its googlevideo URL is rejected later, so player_from_clients alone is not enough.
        for client_type in [ClientType::Ios, ClientType::Android] {
            let query = self.client.query();
            let player = match query.player_from_client(video_id, client_type).await {
                Ok(player) => player,
                Err(e) => {
                    failures.push(format!("{client_type:?}: player request failed: {e}"));
                    continue;
                }
            };

            if player.drm.is_some() && player.audio_streams.iter().all(|s| !s.drm_systems.is_empty()) {
                failures.push(format!("{client_type:?}: DRM protected"));
                continue;
            }

            let Some(stream) = choose_stream(&player.audio_streams) else {
                failures.push(format!("{client_type:?}: no compatible audio stream"));
                continue;
            };

            let duration_seconds = stream
                .duration_ms
                .map(|ms| ms as f64 / 1000.0)
                .unwrap_or(player.details.duration as f64);
            let remote = RemoteAudioStream {
                video_id: video_id.to_string(),
                url: stream.url.clone(),
                user_agent: query.user_agent(player.client_type).into_owned(),
                mime: stream.mime.clone(),
            };

            match self.fetch_remote(&remote, Some("bytes=0-1")).await {
                Ok(probe) if probe.status == 200 || probe.status == 206 => {
                    return Ok((remote, stream.bitrate, duration_seconds));
                }
                Ok(probe) => failures.push(format!("{client_type:?}: stream probe HTTP {}", probe.status)),
                Err(e) => failures.push(format!("{client_type:?}: stream probe failed: {e}")),
            }
        }

        Err(format!(
            "Could not obtain a playable YouTube audio stream. {}",
            failures.join(" | ")
        ))
    }

    pub async fn proxy(&self, key: &str, range: Option<String>) -> Result<ProxyAudioResponse, String> {
        let cached = self
            .streams
            .read()
            .await
            .get(key)
            .cloned()
            .ok_or_else(|| "Playback stream is no longer available. Re-select the track.".to_string())?;

        let first = self.fetch_remote(&cached, range.as_deref()).await?;
        if first.status != 403 {
            return Ok(first);
        }

        // googlevideo URLs expire. Refresh once in-place so a long-running app can keep
        // playing without forcing the user to click the track again.
        let (fresh, _, _) = self.resolve_remote(&cached.video_id).await?;
        self.streams.write().await.insert(key.to_string(), fresh.clone());
        self.fetch_remote(&fresh, range.as_deref()).await
    }

    async fn fetch_remote(
        &self,
        stream: &RemoteAudioStream,
        range: Option<&str>,
    ) -> Result<ProxyAudioResponse, String> {
        let mut request = self
            .http
            .get(&stream.url)
            .header(USER_AGENT, &stream.user_agent)
            .header(ORIGIN, "https://www.youtube.com")
            .header(REFERER, "https://www.youtube.com/");

        if let Some(range) = range.filter(|v| !v.trim().is_empty()) {
            request = request.header(RANGE, range);
        } else {
            request = request.header(RANGE, "bytes=0-8388607");
        }

        let response = request
            .send()
            .await
            .map_err(|e| format!("Playback network request failed: {e}"))?;
        let status = response.status().as_u16();
        let headers = response.headers().clone();
        let body = response
            .bytes()
            .await
            .map_err(|e| format!("Playback stream read failed: {e}"))?
            .to_vec();

        Ok(ProxyAudioResponse {
            status,
            content_type: headers
                .get(CONTENT_TYPE)
                .and_then(|v| v.to_str().ok())
                .map(str::to_owned)
                .unwrap_or_else(|| stream.mime.clone()),
            content_length: headers
                .get(CONTENT_LENGTH)
                .and_then(|v| v.to_str().ok())
                .map(str::to_owned),
            content_range: headers
                .get(CONTENT_RANGE)
                .and_then(|v| v.to_str().ok())
                .map(str::to_owned),
            accept_ranges: headers
                .get(ACCEPT_RANGES)
                .and_then(|v| v.to_str().ok())
                .map(str::to_owned)
                .or_else(|| Some("bytes".into())),
            body,
        })
    }
}

async fn proxy_handler(
    State(resolver): State<PlaybackResolverState>,
    Path(key): Path<String>,
    headers: HeaderMap,
) -> Response<Body> {
    let range = headers
        .get(header::RANGE)
        .and_then(|v| v.to_str().ok())
        .map(str::to_owned);

    match resolver.proxy(&key, range.clone()).await {
        Ok(remote) => {
            #[cfg(debug_assertions)]
            println!(
                "YTM media proxy: key={key} range={:?} status={} bytes={} content-range={:?}",
                range, remote.status, remote.body.len(), remote.content_range
            );
            let status = StatusCode::from_u16(remote.status).unwrap_or(StatusCode::BAD_GATEWAY);
            let mut builder = Response::builder()
                .status(status)
                .header(header::CONTENT_TYPE, remote.content_type)
                .header(header::ACCESS_CONTROL_ALLOW_ORIGIN, "*")
                .header(
                    header::ACCESS_CONTROL_EXPOSE_HEADERS,
                    "Content-Length, Content-Range, Accept-Ranges",
                )
                .header(header::CACHE_CONTROL, "no-store");

            if let Some(value) = remote.content_length {
                builder = builder.header(header::CONTENT_LENGTH, value);
            }
            if let Some(value) = remote.content_range {
                builder = builder.header(header::CONTENT_RANGE, value);
            }
            if let Some(value) = remote.accept_ranges {
                builder = builder.header(header::ACCEPT_RANGES, value);
            }

            builder
                .body(Body::from(remote.body))
                .unwrap_or_else(|_| Response::new(Body::from("Failed to build playback response")))
        }
        Err(message) => Response::builder()
            .status(StatusCode::BAD_GATEWAY)
            .header(header::CONTENT_TYPE, "text/plain; charset=utf-8")
            .header(header::ACCESS_CONTROL_ALLOW_ORIGIN, "*")
            .body(Body::from(message))
            .unwrap_or_else(|_| Response::new(Body::from("Playback proxy failure"))),
    }
}

fn choose_stream(streams: &[AudioStream]) -> Option<&AudioStream> {
    // WebView2 handles AAC/MP4 reliably. Keep another non-DRM audio format as fallback.
    streams
        .iter()
        .filter(|s| s.drm_systems.is_empty() && s.mime.starts_with("audio/mp4"))
        .max_by_key(|s| s.bitrate)
        .or_else(|| {
            streams
                .iter()
                .filter(|s| s.drm_systems.is_empty())
                .max_by_key(|s| s.bitrate)
        })
}
