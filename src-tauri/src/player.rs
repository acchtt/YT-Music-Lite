use serde::{Deserialize, Serialize};
use tokio::sync::RwLock;

use crate::{models::TrackVm, playback_resolver::ResolvedAudioStream};

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
            provider: "rustypipe-htmlaudio".into(),
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

    pub async fn queue_track(
        &self,
        track: TrackVm,
        play_now: bool,
        stream: ResolvedAudioStream,
    ) -> PlayerSnapshot {
        let mut s = self.0.write().await;
        s.queue.push(track.clone());
        let index = (s.queue.len() - 1) as i32;
        if play_now || s.current.is_none() {
            activate(&mut s, index, track, stream, play_now);
        }
        s.clone()
    }

    pub async fn adjacent_target(&self, direction: i32) -> Option<(i32, TrackVm, bool)> {
        let s = self.0.read().await;
        if s.queue.is_empty() {
            return None;
        }
        let current = s.current_index.max(0);
        let next = (current + direction).clamp(0, (s.queue.len() - 1) as i32);
        if next == s.current_index {
            return None;
        }
        Some((next, s.queue[next as usize].clone(), s.is_playing))
    }

    pub async fn activate_index(
        &self,
        index: i32,
        track: TrackVm,
        stream: ResolvedAudioStream,
        autoplay: bool,
    ) -> PlayerSnapshot {
        let mut s = self.0.write().await;
        activate(&mut s, index, track, stream, autoplay);
        s.clone()
    }

    pub async fn control_simple(&self, action: &str, value: Option<f64>) -> PlayerSnapshot {
        let mut s = self.0.write().await;
        match action {
            "play_pause" => {
                if s.playable {
                    s.is_playing = !s.is_playing;
                    s.notice = if s.is_playing { "Playing" } else { "Paused" }.into();
                }
            }
            "seek" => {
                if let Some(v) = value {
                    s.position = v.clamp(0.0, s.duration.max(0.0));
                }
            }
            "volume" => {
                if let Some(v) = value {
                    s.volume = v.clamp(0.0, 1.0);
                }
            }
            _ => {}
        }
        s.clone()
    }

    pub async fn sync_playback(
        &self,
        position: f64,
        duration: f64,
        is_playing: bool,
        volume: f64,
    ) -> PlayerSnapshot {
        let mut s = self.0.write().await;
        if position.is_finite() {
            s.position = position.max(0.0);
        }
        if duration.is_finite() && duration > 0.0 {
            s.duration = duration;
        }
        s.is_playing = is_playing && s.playable;
        if volume.is_finite() {
            s.volume = volume.clamp(0.0, 1.0);
        }
        s.clone()
    }

    pub async fn playback_error(&self, message: String) -> PlayerSnapshot {
        let mut s = self.0.write().await;
        s.is_playing = false;
        s.notice = message;
        s.clone()
    }
}

fn activate(
    s: &mut PlayerSnapshot,
    index: i32,
    track: TrackVm,
    stream: ResolvedAudioStream,
    autoplay: bool,
) {
    s.current_index = index;
    s.current = Some(track);
    s.position = 0.0;
    s.duration = if stream.duration_seconds > 0.0 {
        stream.duration_seconds
    } else {
        s.current
            .as_ref()
            .map(|t| t.duration_seconds)
            .unwrap_or(0.0)
    };
    s.playable = true;
    s.is_playing = autoplay;
    s.stream_url = Some(stream.url);
    s.stream_mime = Some(stream.mime);
    s.stream_bitrate = Some(stream.bitrate);
    s.notice = if autoplay {
        "Resolving complete — starting playback.".into()
    } else {
        "Ready to play.".into()
    };
}
