# Playback provider (Milestone 0.2.2)

Playback is now separate from the YouTube Music data/auth service.

```text
Track selected in React
       ↓
queue_track
       ↓
Rust PlaybackResolver
       ↓
RustyPipe player query
       ↓
short-lived non-DRM audio stream URL
       ↓
main-window HTMLAudioElement
       ↓
sync_playback → Rust PlayerState → main + mini player
```

The provider streams media directly for playback. It does not implement download/save/export features.

## Stream selection

The resolver requests the iOS/Android YouTube player clients because RustyPipe documents those clients as returning unobfuscated stream URLs. It prefers the highest-bitrate non-DRM `audio/mp4` stream for WebView2 compatibility and falls back to another non-DRM audio stream when needed.

## Known limitations in 0.2

- Stream URLs expire and are re-resolved when changing tracks, but long multi-hour pauses do not yet proactively refresh the current URL.
- DRM-only tracks are reported as unsupported by the current provider.
- Gapless playback, crossfade, ReplayGain/normalization and pre-buffering the next item are later milestones.
- Queue population is currently click-driven; radio/autoplay queue expansion comes next.


## 0.2.2 transport path

The WebView no longer requests the resolved googlevideo URL directly. The Rust backend stores the remote URL and the matching RustyPipe client user-agent, then exposes a local `ytmstream` custom protocol. Range requests are forwarded with the expected User-Agent, Origin and Referer headers. This avoids client-UA mismatch and gives the app one place to refresh expired stream URLs.
