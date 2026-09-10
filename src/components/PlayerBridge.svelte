<script lang="ts">
  import { onMount } from "svelte";
  import { getCurrentWindow } from "@tauri-apps/api/window";
  import { completeTrack, registerPlayer, updatePlayer } from "../lib/player";

  let host: HTMLDivElement;
  let yt: any;
  let pending: { id: string; autoplay: boolean } | null = null;

  function loadApi(): Promise<void> {
    if ((window as any).YT?.Player) return Promise.resolve();
    return new Promise(resolve => {
      (window as any).onYouTubeIframeAPIReady = resolve;
      if (!document.querySelector("#yt-iframe-api")) {
        const script = document.createElement("script");
        script.id = "yt-iframe-api";
        script.src = "https://www.youtube.com/iframe_api";
        document.head.appendChild(script);
      }
    });
  }

  onMount(() => {
    let progressTimer = 0;
    let minimizedTimer = 0;
    let destroyed = false;

    void loadApi().then(() => {
      if (destroyed) return;
      yt = new (window as any).YT.Player(host, {
        width: "100%",
        height: "200",
        playerVars: { enablejsapi: 1, playsinline: 1, origin: window.location.origin },
        events: {
          onReady: () => {
            updatePlayer({ ready: true });
            if (pending) pending.autoplay ? yt.loadVideoById(pending.id) : yt.cueVideoById(pending.id);
          },
          onStateChange: ({ data }: { data: number }) => {
            const playing = data === 1;
            updatePlayer({ playing });
            if (data === 0) completeTrack();
          },
          onError: ({ data }: { data: number }) => updatePlayer({ playing: false, error: `YouTube player error ${data}` })
        }
      });

      registerPlayer({
        load(id, autoplay) {
          pending = { id, autoplay };
          if (!yt?.loadVideoById) return;
          autoplay ? yt.loadVideoById(id) : yt.cueVideoById(id);
        },
        play: () => yt?.playVideo?.(),
        pause: () => yt?.pauseVideo?.(),
        seek: seconds => yt?.seekTo?.(seconds, true),
        volume: value => yt?.setVolume?.(Math.round(value * 100))
      });
    }).catch(error => updatePlayer({ error: String(error) }));

    progressTimer = window.setInterval(() => {
      if (!yt?.getCurrentTime) return;
      const position = Number(yt.getCurrentTime()) || 0;
      const duration = Number(yt.getDuration()) || 0;
      updatePlayer({ position, ...(duration > 0 ? { duration } : {}) });
    }, 750);

    minimizedTimer = window.setInterval(async () => {
      if (await getCurrentWindow().isMinimized()) yt?.pauseVideo?.();
    }, 1000);

    const visibility = () => { if (document.hidden) yt?.pauseVideo?.(); };
    document.addEventListener("visibilitychange", visibility);

    return () => {
      destroyed = true;
      clearInterval(progressTimer);
      clearInterval(minimizedTimer);
      document.removeEventListener("visibilitychange", visibility);
      yt?.destroy?.();
    };
  });
</script>

<div class="video-frame"><div bind:this={host}></div></div>
