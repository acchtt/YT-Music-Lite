# YT Music Lite 7 architecture

## Scope

Version 7 targets Windows 10 and Windows 11 only. Tauri hosts one Svelte application in one WebView2 process. The previous React mini-player and hidden playback window have been removed from the active build.

```text
Svelte shell
  ├─ Home, Search, Library, Weekly Mix
  ├─ queue and transport state
  └─ persistent visible YouTube IFrame player
             │
             ├─ YouTube IFrame Player API
             └─ Tauri commands
                    ├─ YouTube Music catalog/account adapter
                    ├─ SQLite listening history
                    └─ signed update service
```

## Memory rules

1. Only the main WebView is persistent.
2. The sign-in WebView exists only during authentication.
3. Artwork uses browser lazy loading and no unbounded JavaScript image cache.
4. Search waits 320 ms and ignores empty requests.
5. SQLite connections are short-lived and use WAL with normal synchronization.
6. No Node, Python, mpv, yt-dlp, or Deno process runs with the app.
7. The Windows release fails QA when its median private working set exceeds 200 MB.

## Discovery

Playback writes compact `play`, `complete`, and `skip` events to `%LocalAppData%`. Weekly Mix ranks favorite artists, fetches a small candidate set, excludes already-heard tracks, caps each artist at three candidates, and falls back to highly scored familiar tracks.

## Data and security boundaries

- Session material remains in the Rust side and is never returned to Svelte.
- The frontend receives normalized view models only.
- Playback uses the supported embedded YouTube player instead of resolved stream URLs.
- Playback pauses when the app is hidden or minimized.
