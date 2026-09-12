# Native Windows architecture

YT Music Lite 7.1 targets 64-bit Windows 10 and Windows 11. The release is built from `client/` with .NET Framework 4.8 and native WinForms controls.

## Runtime process model

1. `YTMusicLite.exe` owns the interface, queue, library, playlists, listening history, and playback state.
2. `yt-dlp.exe` starts only for a search, account sync, or playback URL resolution and exits when the request finishes.
3. One `mpv.exe` process exists while audio is playing. Video decoding is disabled and its demuxer buffer is capped.
4. `deno.exe` may run briefly when `yt-dlp` needs its YouTube challenge solver.

There is no embedded browser or WebView2 process in the production application.

## Data boundaries

- `%LocalAppData%\YTMusicLite\library-v2.json`: saved tracks, playlists, recent tracks, and play counts.
- `%LocalAppData%\YTMusicLite\settings.json`: selected cookie source and playback preferences.
- `%LocalAppData%\YTMusicLite\artwork`: bounded artwork cache.
- Application binaries: installer-selected directory, separate from user data.

## Memory policy

`client/measure.ps1` measures the complete process tree during real local audio decoding. CI fails above 140 MiB combined, above 80 MiB while idle/after stop, or when a child process survives shutdown.

The benchmark is a release gate, not a promise that every online track and Windows configuration will use identical memory.

## Update model

Releases include a native installer and a portable ZIP. The in-app updater reads `ytmlite-v*` releases, downloads the matching ZIP, verifies its SHA-256 digest, and then replaces application files through the separate updater process. User data is not stored in the installation directory.
