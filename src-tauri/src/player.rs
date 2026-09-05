use serde::{Deserialize, Serialize};
use tokio::sync::RwLock;

use crate::{models::TrackVm, official_web_player::WebMediaStatus};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum RepeatMode {
    Off,
    All,
    One,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PlayerSnapshot {
    pub provider: String,
    pub queue: Vec<TrackVm>,
    pub current_index: i32,
    pub current: Option<TrackVm>,
    pub is_playing: bool,
    pub position: f64,
    pub duration: f64,
    pub volume: f64,
    pub shuffle: bool,
    pub repeat: RepeatMode,
    pub playable: bool,
    pub notice: String,
    pub stream_url: Option<String>,
    pub stream_mime: Option<String>,
    pub stream_bitrate: Option<u32>,
}

impl Default for PlayerSnapshot {
    fn default() -> Self {
        Self {
            provider: "youtube-music-official-webview".into(),
            queue: vec![],
            current_index: -1,
            current: None,
            is_playing: false,
            position: 0.0,
            duration: 0.0,
            volume: 0.8,
            shuffle: false,
            repeat: RepeatMode::Off,
            playable: false,
            notice: "Select a track to start playback.".into(),
            stream_url: None,
            stream_mime: None,
            stream_bitrate: None,
        }
    }
}

#[derive(Default)]
pub struct PlayerState(pub RwLock<PlayerSnapshot>);

impl PlayerState {
    pub async fn snapshot(&self) -> PlayerSnapshot {
        self.0.read().await.clone()
    }

    pub async fn queue_track(&self, track: TrackVm, play_now: bool) -> PlayerSnapshot {
        let mut state = self.0.write().await;
        state.queue.push(track.clone());
        let index = (state.queue.len() - 1) as i32;
        if play_now || state.current.is_none() {
            activate(&mut state, index, track, play_now);
        }
        state.clone()
    }

    pub async fn adjacent_target(&self, direction: i32) -> Option<(i32, TrackVm, bool)> {
        let state = self.0.read().await;
        if state.queue.is_empty() {
            return None;
        }
        let current = state.current_index.max(0);
        let next = (current + direction).clamp(0, (state.queue.len() - 1) as i32);
        if next == state.current_index {
            return None;
        }
        Some((next, state.queue[next as usize].clone(), state.is_playing))
    }

    pub async fn activate_index(
        &self,
        index: i32,
        track: TrackVm,
        autoplay: bool,
    ) -> PlayerSnapshot {
        let mut state = self.0.write().await;
        activate(&mut state, index, track, autoplay);
        state.clone()
    }

    pub async fn control_simple(&self, action: &str, value: Option<f64>) -> PlayerSnapshot {
        let mut state = self.0.write().await;
        match action {
            "play_pause" => {
                if state.current.is_some() {
                    state.is_playing = !state.is_playing;
                    state.notice = if state.is_playing { "Playing" } else { "Paused" }.into();
                }
            }
            "play" => {
                if state.current.is_some() {
                    state.is_playing = true;
                    state.notice = "Playing".into();
                }
            }
            "pause" => {
                if state.current.is_some() {
                    state.is_playing = false;
                    state.notice = "Paused".into();
                }
            }
            "seek" => {
                if let Some(value) = value {
                    state.position = value.clamp(0.0, state.duration.max(0.0));
                }
            }
            "volume" => {
                if let Some(value) = value {
                    state.volume = value.clamp(0.0, 1.0);
                }
            }
            _ => {}
        }
        state.clone()
    }

    pub async fn sync_web(&self, media: &WebMediaStatus) -> PlayerSnapshot {
        let mut state = self.0.write().await;

        if media.position.is_finite() {
            state.position = media.position.max(0.0);
        }
        if media.duration.is_finite() && media.duration > 0.0 {
            state.duration = media.duration;
        }
        if media.volume.is_finite() {
            state.volume = media.volume.clamp(0.0, 1.0);
        }

        if let Some(error) = media.error.as_deref().filter(|value| !value.trim().is_empty()) {
            state.playable = false;
            state.is_playing = false;
            state.notice = format!("YouTube Music player error: {error}");
            return state.clone();
        }

        if media.ready {
            state.playable = true;
            state.is_playing = media.is_playing;
            state.notice = if media.ended {
                "Finished".into()
            } else if media.is_playing {
                "Playing through YouTube Music.".into()
            } else {
                "Paused".into()
            };
        } else if state.current.is_some() {
            state.playable = false;
            state.is_playing = false;
            state.notice = if media.page_url.starts_with("https://music.youtube.com")
                || media.page_url == "about:blank"
            {
                "Loading YouTube Music player…".into()
            } else {
                format!("YouTube Music is redirecting playback: {}", media.page_url)
            };
        }

        state.clone()
    }

    // Kept for compatibility with an older frontend during an update boundary.
    pub async fn sync_playback(
        &self,
        position: f64,
        duration: f64,
        is_playing: bool,
        volume: f64,
    ) -> PlayerSnapshot {
        let mut state = self.0.write().await;
        if position.is_finite() {
            state.position = position.max(0.0);
        }
        if duration.is_finite() && duration > 0.0 {
            state.duration = duration;
        }
        state.is_playing = is_playing && state.current.is_some();
        if volume.is_finite() {
            state.volume = volume.clamp(0.0, 1.0);
        }
        state.clone()
    }

    pub async fn playback_error(&self, message: String) -> PlayerSnapshot {
        let mut state = self.0.write().await;
        state.playable = false;
        state.is_playing = false;
        state.notice = message;
        state.clone()
    }
}

fn activate(state: &mut PlayerSnapshot, index: i32, track: TrackVm, autoplay: bool) {
    state.current_index = index;
    state.current = Some(track);
    state.position = 0.0;
    state.duration = state
        .current
        .as_ref()
        .map(|track| track.duration_seconds)
        .unwrap_or(0.0);
    state.playable = false;
    state.is_playing = false;
    state.stream_url = None;
    state.stream_mime = None;
    state.stream_bitrate = None;
    state.notice = if autoplay {
        "Loading YouTube Music player…".into()
    } else {
        "Loading track in YouTube Music…".into()
    };
}
