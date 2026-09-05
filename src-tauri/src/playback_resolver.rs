use std::sync::Arc;

use reqwest::{
    header::{ORIGIN, RANGE, REFERER, USER_AGENT},
    Client,
};
use rustypipe::{
    client::{ClientType, RustyPipe},
    model::AudioStream,
};

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

impl Default for PlaybackResolverState {
    fn default() -> Self {
        Self {
            client: RustyPipe::new(),
            http: Client::builder()
                .redirect(reqwest::redirect::Policy::limited(5))
                .build()
                .expect("failed to create playback HTTP client"),
        }
    }
}

impl PlaybackResolverState {
    /// Resolve a non-DRM AAC/MP4 audio stream and download it into memory for the
    /// native Rodio/Symphonia decoder. WebView2 is no longer involved in playback.
    pub async fn resolve_native(&self, video_id: &str) -> Result<ResolvedNativeAudio, String> {
        if video_id.trim().is_empty() {
            return Err("Track is missing a YouTube video id.".into());
        }

        let mut failures = Vec::new();

        for client_type in [ClientType::Ios, ClientType::Android] {
            let query = self.client.query();
            let player = match query.player_from_client(video_id, client_type).await {
                Ok(player) => player,
                Err(error) => {
                    failures.push(format!("{client_type:?}: player request failed: {error}"));
                    continue;
                }
            };

            if player.drm.is_some() && player.audio_streams.iter().all(|stream| !stream.drm_systems.is_empty()) {
                failures.push(format!("{client_type:?}: DRM protected"));
                continue;
            }

            let Some(stream) = choose_native_stream(&player.audio_streams) else {
                failures.push(format!("{client_type:?}: no non-DRM AAC/MP4 audio stream"));
                continue;
            };

            let user_agent = query.user_agent(player.client_type).into_owned();
            let response = match self
                .http
                .get(&stream.url)
                .header(USER_AGENT, user_agent)
                .header(ORIGIN, "https://www.youtube.com")
                .header(REFERER, "https://www.youtube.com/")
                .header(RANGE, "bytes=0-")
                .send()
                .await
            {
                Ok(response) => response,
                Err(error) => {
                    failures.push(format!("{client_type:?}: audio download failed: {error}"));
                    continue;
                }
            };

            let status = response.status();
            if !status.is_success() {
                failures.push(format!("{client_type:?}: audio download HTTP {}", status.as_u16()));
                continue;
            }

            let bytes = match response.bytes().await {
                Ok(bytes) if bytes.len() >= 1024 => bytes,
                Ok(bytes) => {
                    failures.push(format!("{client_type:?}: audio response was only {} bytes", bytes.len()));
                    continue;
                }
                Err(error) => {
                    failures.push(format!("{client_type:?}: audio body read failed: {error}"));
                    continue;
                }
            };

            let duration_seconds = stream
                .duration_ms
                .map(|milliseconds| milliseconds as f64 / 1000.0)
                .unwrap_or(player.details.duration as f64);

            return Ok(ResolvedNativeAudio {
                bytes: Arc::<[u8]>::from(bytes.to_vec()),
                mime: stream.mime.clone(),
                bitrate: stream.bitrate,
                duration_seconds,
            });
        }

        Err(format!(
            "Could not obtain native AAC audio for this track. {}",
            failures.join(" | ")
        ))
    }
}

fn choose_native_stream(streams: &[AudioStream]) -> Option<&AudioStream> {
    // Native Rodio/Symphonia handles AAC itself, so Windows/WebView2 codec support
    // no longer matters. Prefer the highest-bitrate non-DRM MP4/AAC stream.
    streams
        .iter()
        .filter(|stream| {
            let mime = stream.mime.to_ascii_lowercase();
            stream.drm_systems.is_empty()
                && mime.starts_with("audio/mp4")
                && (mime.contains("mp4a") || !mime.contains("codecs="))
        })
        .max_by_key(|stream| stream.bitrate)
        .or_else(|| {
            streams
                .iter()
                .filter(|stream| {
                    stream.drm_systems.is_empty()
                        && stream.mime.to_ascii_lowercase().starts_with("audio/mp4")
                })
                .max_by_key(|stream| stream.bitrate)
        })
}
