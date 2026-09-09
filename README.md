# YT Music Lite

YT Music Lite is a browser-free Windows music client focused on low memory use. It provides a full desktop interface for YouTube search, saved music, local playlists, a playback queue, local audio, media keys, a mini player, and verified in-app updates.

## Architecture

The production client lives in `client/` and uses only Windows-native controls. It does not host WebView2 or another browser engine.

- `yt-dlp` performs an on-demand YouTube search or resolves one audio URL, then exits.
- If YouTube challenges anonymous playback, Settings can use cookies from Edge, Brave, Chrome, Firefox, or an exported `cookies.txt`; the app stores only that choice/path.
- `mpv` plays audio with bounded buffers and no video pipeline.
- Library, recent music, and playlists are stored locally under `%LocalAppData%\YTMusicLite`.
- Artwork is cached on disk and the decoded in-memory cache is bounded.
- Update archives must pass SHA-256 verification before installation.

## Build

On Windows with .NET Framework 4.8 and PowerShell:

```powershell
.\client\build.ps1
```

For playback, place `mpv.exe`, `yt-dlp.exe`, and `deno.exe` beside `YTMusicLite.exe`. Release builds include all three.

Run the complete local-audio playback, process-cleanup, and memory test with:

```powershell
.\client\measure.ps1
```

## Keyboard and media controls

- `Ctrl+K` or `Ctrl+L` focuses Search.
- `Space` toggles play/pause when focus is outside a text field.
- `Alt+Left` and `Alt+Right` move through page history.
- `Ctrl+M` opens the mini player.
- Hardware play/pause, previous, and next media keys are supported.

The older Tauri, WebView2, and prototype folders are retained only as repository history and are not part of the production build or release.
