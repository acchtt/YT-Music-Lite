# YTM Desktop architecture — Milestone 0.2

## Runtime components

```text
┌────────────────────────────────────────────────────────────┐
│ React / TypeScript                                         │
│                                                            │
│ Home  Search  Library  Settings                            │
│                 │                                          │
│ Bottom Player ──┼──────────── Mini Player                  │
│                 │                ▲                          │
│          useAudioEngine             shared player events   │
└─────────────────┼──────────────────────────────────────────┘
                  │ Tauri IPC
┌─────────────────▼──────────────────────────────────────────┐
│ Rust / Tauri                                               │
│                                                            │
│ MusicServiceState                                          │
│   browser.json → ytmusic-api → account data               │
│                                                            │
│ PlaybackResolverState                                      │
│   video id → RustyPipe → short-lived audio stream         │
│                                                            │
│ PlayerState                                                │
│   queue/current/play state/position/volume                 │
│                                                            │
│ UpdateService                                              │
│   signed HTTPS manifest → download → verify → install      │
└────────────────────────────────────────────────────────────┘
```

## Separation rules

1. React never receives the contents of `browser.json`.
2. Account-data calls stay in `MusicServiceState`.
3. Stream resolution stays in `PlaybackResolverState`, so it can be replaced without changing Home/Search/Library.
4. Only the main window owns the audio element. The mini-player is a controller/view over Rust `PlayerState`, preventing two windows from playing the same track simultaneously.
5. Update packages must pass Tauri signature verification before installation.

## Playback state flow

```text
click TrackCard
  → queue_track(track, true)
  → resolve stream
  → PlayerState(streamUrl, isPlaying=true)
  → player-state event
  → useAudioEngine loads stream
  → timeupdate/play/pause events
  → sync_playback(...)
  → PlayerState
  → player-state event to main + mini
```

## Updates

The app has a fixed stable update source at `acchtt/YTM-Desktop` GitHub Releases. No per-user update URL or signing key is entered in Settings.

```text
Settings > Check for updates
        ↓
GitHub Releases API
        ↓
newer app-vX.Y.Z?
        ↓
Windows *-setup.exe + matching .sha256
        ↓
Rust download + progress
        ↓
SHA-256 verification
        ↓
wait for YTM Desktop to exit
        ↓
NSIS /S
        ↓
relaunch installed build
```

The GitHub Actions workflow builds the NSIS bundle and publishes the checksum automatically for every `app-v*` tag. This updater is intentionally separate from the YouTube Music account/session and playback services.
