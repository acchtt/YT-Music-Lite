# YT Music Lite — Native Windows

YT Music Lite 7.1 is a browser-free Windows music client focused on low memory use. It provides YouTube search, saved music, local playlists, a playback queue, a history-aware Home view, local audio, media keys, a mini player, and verified in-app updates.

## Production architecture

The production client lives in `client/` and uses Windows-native WinForms controls. It does not host WebView2 or another continuously running browser engine.

- `yt-dlp` performs an on-demand search or resolves one audio URL, then exits.
- `mpv` plays audio only, with the video pipeline disabled and bounded network buffers.
- Settings can use cookies from Edge, Brave, Chrome, Firefox, or an exported `cookies.txt` when YouTube requires sign-in.
- Library, recent tracks, playlists, and playback preferences are stored under `%LocalAppData%\YTMusicLite`.
- Artwork uses bounded memory and disk caches.
- Update archives must pass SHA-256 verification before installation.

The Tauri, Svelte, and older WebView2 sources are retained as development history; they are not part of the production build or release.

## Playback controls

- Previous (restarts the current track after three seconds)
- Play/pause
- Next
- Stop
- Seek timeline
- Volume and mute
- Shuffle
- Repeat all and repeat one

These controls are available from the main player and mini player. The tray menu and Windows media keys cover the relevant transport and volume actions.

## Install

Download `YTMusicLite-v7.1.1-win-x64-setup.exe` from the latest GitHub release. A portable ZIP is also provided and is used by the verified in-app updater.

User data is stored separately from the installation directory, so upgrading preserves the library and playlists.

## Build

On Windows with .NET Framework 4.8 and PowerShell:

```powershell
.\client\build.ps1
```

Place `mpv.exe`, `yt-dlp.exe`, and `deno.exe` beside `YTMusicLite.exe` for playback. Release builds include all three.

Run the complete native playback, process-cleanup, and memory test with:

```powershell
.\client\measure.ps1
```

The test rejects a combined client/player peak above 140 MiB, an idle client peak above 80 MiB, or leftover playback helpers after shutdown.

## Keyboard and media controls

- `Ctrl+K` or `Ctrl+L`: focus Search
- `Space`: play/pause outside text fields
- `Ctrl+Left` / `Ctrl+Right`: previous/next
- `Ctrl+Shift+S`: shuffle
- `Ctrl+Shift+R`: cycle repeat mode
- `Alt+Left` / `Alt+Right`: page history
- `Ctrl+M`: mini player
- Hardware play/pause, previous, next, stop, mute, volume-down, and volume-up keys are supported

## Playback-service note

This low-memory architecture resolves YouTube audio through `yt-dlp`, which is unofficial and can require updates when YouTube changes. Only play content you are authorized to access and follow the service's terms.
