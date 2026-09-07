# Native playback feasibility prototype

Separate Windows x64 executable, with no WebView2 reference. This is a playback and memory experiment, not a replacement release. The existing app is untouched.

## Run

Install mpv, yt-dlp and Deno from their official projects (or your trusted package manager). Put executables beside `build/YTMusicLite.Native.exe` or on PATH. Deno is needed by yt-dlp's YouTube challenge solver; keep both updated.

Run `./build.ps1` in Windows PowerShell, then open `build/YTMusicLite.Native.exe`. Paste one YouTube HTTPS URL and click Play, or open a local audio file. Pause/resume controls mpv through a unique Windows named pipe. Stop and closing the app terminate its player and any active resolver subtree. The resolver exits after obtaining the audio URL. No browser profile or account cookies are accessed.

The mpv demuxer forward buffer is capped at 8 MiB with no backward cache. This is a buffer limit, not a total RAM limit. Playback URLs are resolved again on each Play. YouTube can reject extraction or direct playback; this prototype does not implement token providers, request-header forwarding, account login, search, playlists, or queue restoration.

## Measure

Run `./measure.ps1`. It generates a 30-second PCM tone, builds the app, starts real mpv decoding with a null audio output (for CI), checks playback-time and pause over IPC, then stops and exits. The script samples the app and descendants including helper processes, writes per-process and combined working-set/private-byte CSVs, and rejects leftover processes.

Use `./measure.ps1 -Source 'https://www.youtube.com/watch?v=…'` to exercise a permitted YouTube track with installed resolver dependencies. A failed lookup must not be reported as a successful low-memory result. One-second/half-second sampling can miss brief process peaks. CI local-tone results do not measure YouTube resolution, network buffering, physical audio output or account features. Compare repeated runs on the same Windows machine with the current app before setting a release memory target.

The automated sequence requests playback at 3 seconds, reads playback-time at 8, pauses at 10, stops at 16 and exits at 21. Online resolution can shift wall-clock phases; use the stage log as well as the process names rather than assuming exact phase times.
