import type { Track } from "./music";

export type RepeatMode = "off" | "all" | "one";

export type PlayerState = {
  provider: string;
  queue: Track[];
  currentIndex: number;
  current: Track | null;
  isPlaying: boolean;
  position: number;
  duration: number;
  volume: number;
  shuffle: boolean;
  repeat: RepeatMode;
  playable: boolean;
  notice: string;
  streamUrl?: string | null;
  streamMime?: string | null;
  streamBitrate?: number | null;
};
