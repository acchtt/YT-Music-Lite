import { invoke } from "@tauri-apps/api/core";
import type { AuthStatus, HomeSection, Playlist, SearchResults, Track, UpdateStatus } from "./types";

const call = <T>(command: string, args: Record<string, unknown> = {}) => invoke<T>(command, args);

export const api = {
  authStatus: () => call<AuthStatus>("auth_status"),
  startLogin: () => call<void>("start_web_login"),
  pollLogin: () => call<AuthStatus | null>("poll_web_login"),
  clearAuth: () => call<AuthStatus>("clear_auth"),
  home: () => call<HomeSection[]>("get_home"),
  search: (query: string) => call<SearchResults>("search_music", { query }),
  liked: (limit = 100) => call<Track[]>("get_liked_songs", { limit }),
  playlists: (limit = 100) => call<Playlist[]>("get_library_playlists", { limit }),
  playlistTracks: (playlistId: string) => call<Track[]>("get_playlist_tracks", { playlistId }),
  history: () => call<Track[]>("get_history"),
  discover: (limit = 30) => call<Track[]>("get_discover_weekly", { limit }),
  recordListen: (track: Track, eventType: "play" | "complete" | "skip" | "like") =>
    call<void>("record_listen", { track, eventType }),
  checkUpdate: () => call<UpdateStatus>("check_for_updates"),
  installUpdate: () => call<void>("install_update")
};
