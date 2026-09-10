import { get, writable } from "svelte/store";
import type { Track } from "./types";
import { api } from "./api";

export type PlayerState = {
  queue: Track[];
  index: number;
  current: Track | null;
  playing: boolean;
  position: number;
  duration: number;
  volume: number;
  ready: boolean;
  error: string;
};

type Adapter = {
  load(videoId: string, autoplay: boolean): void;
  play(): void;
  pause(): void;
  seek(seconds: number): void;
  volume(value: number): void;
};

const initial: PlayerState = { queue: [], index: -1, current: null, playing: false, position: 0, duration: 0, volume: .82, ready: false, error: "" };
export const player = writable<PlayerState>(initial);
let adapter: Adapter | null = null;

export function registerPlayer(value: Adapter) {
  adapter = value;
  const state = get(player);
  if (state.current) value.load(state.current.videoId, state.playing);
  value.volume(state.volume);
}

export function playTrack(track: Track) {
  const state = get(player);
  const existing = state.queue.findIndex(item => item.videoId === track.videoId);
  const queue = existing >= 0 ? state.queue : [...state.queue, track];
  const index = existing >= 0 ? existing : queue.length - 1;
  player.set({ ...state, queue, index, current: track, playing: true, position: 0, duration: track.durationSeconds, error: "" });
  adapter?.load(track.videoId, true);
  void api.recordListen(track, "play").catch(() => undefined);
}

export function toggle() {
  const state = get(player);
  if (!state.current) return;
  state.playing ? adapter?.pause() : adapter?.play();
}

export function pause() { adapter?.pause(); }
export function seek(seconds: number) { adapter?.seek(seconds); }
export function setVolume(value: number) { adapter?.volume(value); player.update(s => ({ ...s, volume: value })); }

export function adjacent(direction: -1 | 1) {
  const state = get(player);
  const index = state.index + direction;
  if (index < 0 || index >= state.queue.length) return;
  if (direction === 1 && state.current && state.duration > 0 && state.position / state.duration < .2) {
    void api.recordListen(state.current, "skip").catch(() => undefined);
  }
  playTrack(state.queue[index]);
}

export function updatePlayer(patch: Partial<PlayerState>) { player.update(state => ({ ...state, ...patch })); }
export function completeTrack() {
  const state = get(player);
  if (state.current) void api.recordListen(state.current, "complete").catch(() => undefined);
  adjacent(1);
}
