use std::sync::{
    atomic::{AtomicU64, Ordering},
    Arc,
};

use rustypipe::{
    client::RustyPipe,
    model::AudioCodec,
    param::StreamFilter,
};
use rustypipe_downloader::Downloader;

#[derive(Clone)]
pub struct ResolvedNativeAudio {
    pub bytes: Arc<[u8]>,
    pub mime: String,
    pub bitrate: u32,
    pub duration_seconds: f64,
}

#[derive(Clone)]
pub struct PlaybackResolverState {
    downloader: Downloader,
    sequence: Arc<AtomicU64>,
}

impl Default for PlaybackResolverState {
    fn default() -> Self {
        // Keep RustyPipe and its downloader on the same upstream revision. The
        // downloader owns the YouTube-specific retry logic: current client order,
        // visitor-data invalidation on 403, client rotation and chunked googlevideo
        // requests. Playback still remains in-memory/native after the temporary
        // transfer completes.
        let client = RustyPipe::new();
        let downloader = Downloader::builder()
            .rustypipe(&client)
            .n_retries(3)
            .build();

        Self {
            downloader,
            sequence: Arc::new(AtomicU64::new(1)),
        }
    }
}

impl PlaybackResolverState {
    /// Fetch a non-DRM AAC/M4A stream through RustyPipe's maintained downloader,
    /// read it into memory, delete the temporary transfer file, then hand the bytes
    /// to the native Rodio/Symphonia player. WebView2 never handles media playback.
    pub async fn resolve_native(&self, video_id: &str) -> Result<ResolvedNativeAudio, String> {
        if video_id.trim().is_empty() {
            return Err("Track is missing a YouTube video id.".into());
        }

        let safe_id: String = video_id
            .chars()
            .map(|c| if c.is_ascii_alphanumeric() || c == '-' || c == '_' { c } else { '_' })
            .collect();
        let sequence = self.sequence.fetch_add(1, Ordering::Relaxed);
        let temp_path = std::env::temp_dir().join(format!(
            "ytm-desktop-native-{}-{}-{}.m4a",
            std::process::id(),
            safe_id,
            sequence
        ));
        let temp_part = temp_path.with_extension("m4a.part");

        // Clean stale files from a previously interrupted transfer. The filename is
        // process-id + sequence scoped, so this never targets another app's media.
        let _ = tokio::fs::remove_file(&temp_path).await;
        let _ = tokio::fs::remove_file(&temp_part).await;

        let filter = StreamFilter::new()
            .no_video()
            .audio_codecs(vec![AudioCodec::Mp4a]);

        let result = match self
            .downloader
            .id(video_id.to_owned())
            .stream_filter(filter.clone())
            .to_file(temp_path.clone())
            .download()
            .await
        {
            Ok(result) => result,
            Err(error) => {
                let _ = tokio::fs::remove_file(&temp_path).await;
                let _ = tokio::fs::remove_file(&temp_part).await;
                return Err(format!(
                    "Could not fetch native YouTube audio after automatic retries: {error}"
                ));
            }
        };

        let actual_path = result.dest.clone();
        let actual_part = {
            let mut extension = actual_path
                .extension()
                .map(|value| value.to_os_string())
                .unwrap_or_default();
            extension.push(".part");
            actual_path.with_extension(extension)
        };

        let (mime, bitrate, duration_seconds) = {
            let (_, audio) = result.player_data.select_video_audio_stream(&filter);
            let audio = audio.ok_or_else(|| {
                "RustyPipe downloaded the track but did not return the selected AAC stream metadata."
                    .to_string()
            })?;

            let duration = audio
                .duration_ms
                .map(|milliseconds| milliseconds as f64 / 1000.0)
                .unwrap_or(result.player_data.details.duration as f64);

            (audio.mime.clone(), audio.average_bitrate, duration)
        };

        let bytes = match tokio::fs::read(&actual_path).await {
            Ok(bytes) if bytes.len() >= 1024 => bytes,
            Ok(bytes) => {
                let _ = tokio::fs::remove_file(&actual_path).await;
                let _ = tokio::fs::remove_file(&actual_part).await;
                return Err(format!(
                    "Native audio transfer completed but contained only {} bytes.",
                    bytes.len()
                ));
            }
            Err(error) => {
                let _ = tokio::fs::remove_file(&actual_path).await;
                let _ = tokio::fs::remove_file(&actual_part).await;
                return Err(format!("Could not read native audio transfer: {error}"));
            }
        };

        let _ = tokio::fs::remove_file(&actual_path).await;
        let _ = tokio::fs::remove_file(&actual_part).await;

        Ok(ResolvedNativeAudio {
            bytes: Arc::<[u8]>::from(bytes),
            mime,
            bitrate,
            duration_seconds,
        })
    }
}
