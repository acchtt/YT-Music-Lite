# Native playback

YT Music Lite uses a short-lived resolver and a separate audio-only player.

## Start sequence

1. The selected track becomes the current queue item.
2. A local file is passed directly to `mpv`.
3. A YouTube URL is resolved by `yt-dlp`; the resolver exits after returning the audio URL.
4. `mpv` starts with video disabled, an 8 MiB forward demuxer cap, no backward cache, and a unique named IPC pipe.
5. The client polls position, duration, and pause state through the pipe.

## Commands

- Play/pause: `cycle pause`
- Seek: `set_property time-pos`
- Volume/mute: `set_property volume`
- Stop: terminate the player subtree and reset playback state
- Previous/next: update the native queue and resolve the selected track
- Shuffle: randomize only upcoming queue items while preserving the current item
- Repeat one: restart the current queue item after natural completion
- Repeat all: wrap at either end of the queue

Closing the app, stopping playback, starting another track, or timing out a resolver terminates the relevant process tree.

## Service limitations

YouTube URL resolution is unofficial and may break when the service changes. Authenticated playback can read cookies from a browser profile or an exported cookie file selected in Settings. Resolver and account-sync processes remain on demand rather than persistent.
