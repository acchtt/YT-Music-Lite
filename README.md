# YT Music Lite 7 — Windows

YT Music Lite 7 is a Windows-only, Spotify-inspired YouTube Music desktop client built with Tauri 2, Svelte 5, Rust, SQLite, and the YouTube IFrame Player API.

## Runtime design

- One WebView2 window and one persistent player instance.
- Visible 200px YouTube player surface in the Now Playing panel.
- Playback pauses when the app is minimized.
- Rust owns account/catalog access and local SQLite listening history.
- Svelte owns the shell, queue, transport controls, and bounded UI state.
- Weekly Mix uses locally weighted listening history to expand recommendations around favorite artists.
- Search is debounced and track artwork is lazy-loaded.

The `client/`, `yt-music-lite/`, and `native-prototype/` directories are retained as previous implementation history. Version 7 builds from `src/` and `src-tauri/`.

## Requirements

- Windows 10 or Windows 11
- WebView2 Runtime
- Node.js 20.19+
- Rust stable with the MSVC toolchain
- Visual Studio Build Tools with Desktop development with C++

## Development

```powershell
npm ci
npm run tauri:dev
```

## Build the Windows installer

```powershell
.\scripts\build-windows-v7.ps1
```

The NSIS installer is written under `src-tauri\target\release\bundle\nsis`.

## RAM release gate

Start a release build, connect an account, load a large list, begin playback, and run:

```powershell
.\scripts\measure-windows-v7.ps1
```

The test fails if the median private working set exceeds 200 MB. Run it without DevTools and after Windows has finished initializing WebView2.

## Playback notes

The player uses YouTube's supported embedded-player interface. It intentionally does not extract audio URLs, download media, or continue playback while minimized. The packaged app must be tested to confirm WebView2 sends the player identification/referrer expected by YouTube.
