use std::{
    fs,
    path::PathBuf,
    sync::Arc,
};

use reqwest::{
    header::{ORIGIN, REFERER, USER_AGENT},
    Client, Url,
};
use rustypipe::{
    client::{ClientType, RustyPipe},
    model::AudioCodec,
    param::StreamFilter,
};

const MEDIA_CHUNK_SIZE: u64 = 8 * 1024 * 1024;
const MAX_FETCH_ATTEMPTS: usize = 4;

#[derive(Clone)]
pub struct ResolvedNativeAudio {
    pub bytes: Arc<[u8]>,
    pub mime: String,
    pub bitrate: u32,
    pub duration_seconds: f64,
}

#[derive(Clone)]
pub struct PlaybackResolverState {
    client: RustyPipe,
    http: Client,
}

fn playback_storage_dir() -> PathBuf {
    let base = std::env::var_os("LOCALAPPDATA")
        .or_else(|| std::env::var_os("APPDATA"))
        .map(PathBuf::from)
        .unwrap_or_else(std::env::temp_dir);
    base.join("YTM Desktop").join("rustypipe")
}

fn bundled_botguard_path() -> Option<PathBuf> {
    let exe_dir = std::env::current_exe().ok()?.parent()?.to_path_buf();

    #[cfg(target_os = "windows")]
    let names = [
        "rustypipe-botguard.exe",
        "rustypipe-botguard-x86_64-pc-windows-msvc.exe",
    ];

    #[cfg(not(target_os = "windows"))]
    let names = ["rustypipe-botguard", "rustypipe-botguard-x86_64-unknown-linux-gnu"];

    names
        .into_iter()
        .map(|name| exe_dir.join(name))
        .find(|path| path.is_file())
}

fn stream_has_po_token(stream_url: &str) -> bool {
    Url::parse(stream_url)
        .ok()
        .is_some_and(|url| {
            url.query_pairs()
                .any(|(key, value)| key == "pot" && !value.is_empty())
        })
}

impl Default for PlaybackResolverState {
    fn default() -> Self {
        let storage_dir = playback_storage_dir();
        if let Err(error) = fs::create_dir_all(&storage_dir) {
            eprintln!(
                "Could not create RustyPipe playback cache directory {}: {error}",
                storage_dir.display()
            );
        }

        // rustypipe-botguard generates the PO tokens required by current YouTube
        // web player streams. The release build bundles it next to the main exe.
        // po_token_cache reuses the short-lived session token and snapshot data so
        // starting subsequent tracks is much cheaper than solving BotGuard anew.
        let mut builder = RustyPipe::builder()
            .storage_dir(storage_dir.clone())
            .po_token_cache();
        if let Some(botguard) = bundled_botguard_path() {
            builder = builder.botguard_bin(botguard);
        }

        let client = builder.build().unwrap_or_else(|error| {
            eprintln!("Could not initialize RustyPipe BotGuard support: {error}");
            RustyPipe::builder()
                .storage_dir(storage_dir)
                .no_botguard()
                .build()
                .expect("failed to create fallback RustyPipe client")
        });

        Self {
            client,
            http: Client::builder()
                .redirect(reqwest::redirect::Policy::limited(5))
                .build()
                .expect("failed to create playback HTTP client"),
        }
    }
}

#[derive(Debug)]
enum MediaFetchError {
    Http(u16),
    Network(String),
    Invalid(String),
}

impl std::fmt::Display for MediaFetchError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Http(status) => write!(f, "HTTP {status}"),
            Self::Network(message) | Self::Invalid(message) => f.write_str(message),
        }
    }
}

impl PlaybackResolverState {
    /// Resolve a current non-DRM AAC/M4A stream with RustyPipe, fetch the
    /// googlevideo media in bounded chunks, and return the complete bytes to the
    /// native Rodio/Symphonia player. WebView2 is not involved in playback.
    pub async fn resolve_native(&self, video_id: &str) -> Result<ResolvedNativeAudio, String> {
        if video_id.trim().is_empty() {
            return Err("Track is missing a YouTube video id.".into());
        }

        let filter = StreamFilter::new()
            .no_video()
            .audio_codecs(vec![AudioCodec::Mp4a]);
        let botguard_version = self.client.version_botguard().await;
        let botguard_label = botguard_version.as_deref().unwrap_or("unavailable");
        let mut failed_client: Option<ClientType> = None;
        let mut failures = Vec::new();

        for attempt in 0..MAX_FETCH_ATTEMPTS {
            let query = self.client.query();
            let mut clients = query.player_client_order().to_vec();

            // If the last player/media attempt failed, start with the next client.
            // With the bundled PO-token helper the normal order begins with Desktop,
            // followed by iOS/TV fallbacks. Calling one explicit client per attempt
            // keeps runtime diagnostics honest instead of hiding an internal fallback.
            if let Some(failed) = failed_client {
                if let Some(index) = clients.iter().position(|client| *client == failed) {
                    let split = index + 1;
                    if split < clients.len() {
                        let mut rotated = clients[split..].to_vec();
                        rotated.extend_from_slice(&clients[..split]);
                        clients = rotated;
                    }
                }
            }

            let Some(client_type) = clients.first().copied() else {
                failures.push(format!("attempt {}: RustyPipe exposed no player clients", attempt + 1));
                break;
            };

            let player = match query.player_from_client(video_id, client_type).await {
                Ok(player) => player,
                Err(error) => {
                    failures.push(format!(
                        "attempt {} {client_type:?}: player request failed: {error}; botguard={botguard_label}",
                        attempt + 1
                    ));
                    failed_client = Some(client_type);
                    tokio::time::sleep(std::time::Duration::from_millis(250)).await;
                    continue;
                }
            };

            if player.drm.is_some()
                && player
                    .audio_streams
                    .iter()
                    .all(|stream| !stream.drm_systems.is_empty())
            {
                return Err("This track only exposed DRM-protected audio streams.".into());
            }

            let (_, audio) = player.select_video_audio_stream(&filter);
            let Some(audio) = audio else {
                failures.push(format!(
                    "attempt {} {client_type:?}: no non-DRM AAC/M4A stream; botguard={botguard_label}",
                    attempt + 1
                ));
                failed_client = Some(client_type);
                continue;
            };

            let user_agent = query.user_agent(client_type).into_owned();
            let stream_url = audio.url.clone();
            let stream_size = audio.size;
            let has_po_token = stream_has_po_token(&stream_url);
            let mime = audio.mime.clone();
            let bitrate = audio.average_bitrate;
            let duration_seconds = audio
                .duration_ms
                .map(|milliseconds| milliseconds as f64 / 1000.0)
                .unwrap_or(player.details.duration as f64);
            let visitor_data = player.visitor_data.clone();

            match self
                .fetch_googlevideo(&stream_url, stream_size, &user_agent)
                .await
            {
                Ok(bytes) if bytes.len() >= 1024 => {
                    return Ok(ResolvedNativeAudio {
                        bytes: Arc::<[u8]>::from(bytes),
                        mime,
                        bitrate,
                        duration_seconds,
                    });
                }
                Ok(bytes) => {
                    failures.push(format!(
                        "attempt {} {client_type:?}: media response was only {} bytes; size={stream_size}; pot={has_po_token}; botguard={botguard_label}",
                        attempt + 1,
                        bytes.len()
                    ));
                    failed_client = Some(client_type);
                }
                Err(MediaFetchError::Http(status)) if matches!(status, 401 | 403 | 410) => {
                    // RustyPipe associates 403s with stale/bad visitor data. Remove
                    // it from the cache before asking YouTube for a fresh player URL.
                    if status == 403 {
                        if let Some(visitor_data) = visitor_data.as_deref() {
                            query.remove_visitor_data(visitor_data);
                        }
                    }
                    failures.push(format!(
                        "attempt {} {client_type:?}: media HTTP {status}; size={stream_size}; pot={has_po_token}; botguard={botguard_label}; refreshing player session",
                        attempt + 1
                    ));
                    failed_client = Some(client_type);
                }
                Err(error) => {
                    failures.push(format!(
                        "attempt {} {client_type:?}: {error}; size={stream_size}; pot={has_po_token}; botguard={botguard_label}",
                        attempt + 1
                    ));
                    failed_client = Some(client_type);
                }
            }

            tokio::time::sleep(std::time::Duration::from_millis(300)).await;
        }

        Err(format!(
            "Could not fetch native YouTube audio after automatic client/session retries. {}",
            failures.join(" | ")
        ))
    }

    async fn fetch_googlevideo(
        &self,
        stream_url: &str,
        expected_size: u64,
        user_agent: &str,
    ) -> Result<Vec<u8>, MediaFetchError> {
        if expected_size == 0 {
            let response = self
                .http
                .get(stream_url)
                .header(USER_AGENT, user_agent)
                .header(ORIGIN, "https://www.youtube.com")
                .header(REFERER, "https://www.youtube.com/")
                .send()
                .await
                .map_err(|error| MediaFetchError::Network(format!("media request failed: {error}")))?;
            let status = response.status();
            if !status.is_success() {
                return Err(MediaFetchError::Http(status.as_u16()));
            }
            return response
                .bytes()
                .await
                .map(|bytes| bytes.to_vec())
                .map_err(|error| MediaFetchError::Network(format!("media body failed: {error}")));
        }

        let capacity = usize::try_from(expected_size)
            .unwrap_or(16 * 1024 * 1024)
            .min(128 * 1024 * 1024);
        let mut output = Vec::with_capacity(capacity);
        let mut offset = 0u64;

        while offset < expected_size {
            let end = (offset + MEDIA_CHUNK_SIZE - 1).min(expected_size - 1);
            let mut ranged_url = Url::parse(stream_url)
                .map_err(|error| MediaFetchError::Invalid(format!("invalid media URL: {error}")))?;
            ranged_url
                .query_pairs_mut()
                .append_pair("range", &format!("{offset}-{end}"));

            let response = self
                .http
                .get(ranged_url)
                .header(USER_AGENT, user_agent)
                .header(ORIGIN, "https://www.youtube.com")
                .header(REFERER, "https://www.youtube.com/")
                .send()
                .await
                .map_err(|error| MediaFetchError::Network(format!("media chunk request failed: {error}")))?;
            let status = response.status();
            if !status.is_success() {
                return Err(MediaFetchError::Http(status.as_u16()));
            }

            let chunk = response
                .bytes()
                .await
                .map_err(|error| MediaFetchError::Network(format!("media chunk read failed: {error}")))?;
            if chunk.is_empty() {
                return Err(MediaFetchError::Invalid(format!(
                    "YouTube returned an empty media chunk at byte {offset}"
                )));
            }

            output.extend_from_slice(&chunk);
            offset = offset.saturating_add(chunk.len() as u64);

            // A server may ignore the range parameter and return the complete media
            // in one response. In that case the download is already complete.
            if output.len() as u64 >= expected_size {
                break;
            }
        }

        if output.len() as u64 > expected_size {
            output.truncate(expected_size as usize);
        }

        Ok(output)
    }
}
