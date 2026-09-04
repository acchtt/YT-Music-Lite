import { useCallback, useEffect, useState } from "react";
import { listen } from "@tauri-apps/api/event";
import { api } from "../lib/tauri";
import type { PlayerState } from "../types/player";
import type { Track } from "../types/music";

const empty: PlayerState = {
  provider: "rustypipe-htmlaudio",
  queue: [],
  currentIndex: -1,
  current: null,
  isPlaying: false,
  position: 0,
  duration: 0,
  volume: 0.8,
  shuffle: false,
  repeat: "off",
  playable: false,
  notice: "Select a track to start playback.",
  streamUrl: null,
  streamMime: null,
  streamBitrate: null
};

export function usePlayer() {
  const [state, setState] = useState<PlayerState>(empty);

  useEffect(() => {
    api.player().then(setState).catch(() => {});
    const pending = listen<PlayerState>("player-state", (event) => setState(event.payload));
    return () => { void pending.then((unlisten) => unlisten()); };
  }, []);

  const control = useCallback(async (action: string, value?: number) => {
    try { setState(await api.control(action, value)); }
    catch { setState(await api.player()); }
  }, []);

  const queue = useCallback(async (track: Track) => {
    try { setState(await api.queueTrack(track, true)); }
    catch { setState(await api.player()); }
  }, []);

  return { state, control, queue };
}
