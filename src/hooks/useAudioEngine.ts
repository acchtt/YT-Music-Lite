import { useEffect, useRef } from "react";
import { api } from "../lib/tauri";
import type { PlayerState } from "../types/player";

export function useAudioEngine(state: PlayerState) {
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const streamRef = useRef<string | null>(null);
  const lastSync = useRef(0);
  const desiredPlaying = useRef(false);

  desiredPlaying.current = state.isPlaying;

  useEffect(() => {
    // Keep the element in the DOM. WebView2 is more consistent with media lifecycle,
    // autoplay policy and range loading when the HTMLMediaElement is attached.
    const audio = document.createElement("audio");
    audio.preload = "auto";
    audio.volume = state.volume;
    audio.setAttribute("playsinline", "");
    audio.setAttribute("data-ytm-audio-engine", "true");
    audio.style.display = "none";
    document.body.appendChild(audio);
    audioRef.current = audio;

    const sync = (force = false) => {
      const now = performance.now();
      if (!force && now - lastSync.current < 450) return;
      lastSync.current = now;
      void api.syncPlayback(
        Number.isFinite(audio.currentTime) ? audio.currentTime : 0,
        Number.isFinite(audio.duration) ? audio.duration : state.duration,
        !audio.paused && !audio.ended,
        audio.volume
      ).catch(() => {});
    };

    const reportPlayError = (prefix: string, error: unknown) => {
      const detail = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
      void api.playbackError(`${prefix}: ${detail}`).catch(() => {});
    };

    const start = () => {
      if (!desiredPlaying.current || !audio.src || !audio.paused) return;
      void audio.play().catch(error => reportPlayError("Could not start playback", error));
    };

    audio.addEventListener("loadedmetadata", start);
    audio.addEventListener("canplay", start);
    audio.addEventListener("timeupdate", () => sync(false));
    audio.addEventListener("durationchange", () => sync(true));
    audio.addEventListener("play", () => sync(true));
    audio.addEventListener("pause", () => sync(true));
    audio.addEventListener("volumechange", () => sync(true));
    audio.addEventListener("ended", () => {
      sync(true);
      void api.control("next").catch(() => {});
    });
    audio.addEventListener("error", () => {
      const code = audio.error?.code;
      const message = audio.error?.message?.trim();
      const source = audio.currentSrc || audio.src;
      const extra = [code ? `media error ${code}` : "", message || ""].filter(Boolean).join(": ");
      void api.playbackError(
        `Audio playback failed${extra ? ` (${extra})` : ""}${source ? ` [${source}]` : ""}.`
      ).catch(() => {});
    });

    return () => {
      audio.pause();
      audio.removeAttribute("src");
      audio.load();
      audio.remove();
      audioRef.current = null;
    };
    // A single media element owns playback for the main window lifetime.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const audio = audioRef.current;
    const url = state.streamUrl || null;
    if (!audio || !url) return;

    if (streamRef.current !== url) {
      streamRef.current = url;
      audio.pause();
      audio.src = url;
      audio.volume = state.volume;
      audio.load();

      if (state.position > 0) {
        const applyPosition = () => {
          try { audio.currentTime = state.position; } catch { /* metadata may still be settling */ }
        };
        if (audio.readyState >= 1) applyPosition();
        else audio.addEventListener("loadedmetadata", applyPosition, { once: true });
      }

      // loadedmetadata/canplay will retry if WebView2 needs the initial range first.
      if (state.isPlaying) {
        void audio.play().catch(() => {});
      }
      return;
    }

    if (Math.abs(audio.volume - state.volume) > 0.005) audio.volume = state.volume;

    if (Math.abs(audio.currentTime - state.position) > 1.5 && Number.isFinite(state.position)) {
      try { audio.currentTime = state.position; } catch { /* ignore transient seek errors */ }
    }

    if (state.isPlaying && audio.paused) {
      void audio.play().catch(error => {
        const detail = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
        void api.playbackError(`Could not resume playback: ${detail}`).catch(() => {});
      });
    } else if (!state.isPlaying && !audio.paused) {
      audio.pause();
    }
  }, [state.streamUrl, state.isPlaying, state.position, state.volume]);
}
