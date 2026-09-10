# Playback — Windows version 7

The main Svelte layout owns one persistent `PlayerBridge`. It loads the YouTube IFrame Player API once and exposes play, pause, seek, volume, previous, and next through the local player store.

```text
track selected
  → queue/store update
  → PlayerBridge.load(videoId)
  → YouTube IFrame state events
  → transport and progress UI
  → SQLite listening event
```

The 200px player surface stays visible in the Now Playing panel. A timer checks the native Windows minimize state and pauses playback when minimized. The title-bar minimize action pauses immediately as well.

## Packaged-build gate

Before release, verify on Windows that:

1. WebView2 loads `https://www.youtube.com/iframe_api` under the production CSP.
2. Playback starts from a user click.
3. Seek, volume, next, and previous work.
4. Minimize pauses within one second.
5. The player request includes the required identifying referrer/origin.
6. Median private working set remains below 200 MB during the standard measurement run.
