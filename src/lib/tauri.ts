import { invoke } from "@tauri-apps/api/core";
import type { Album, Artist, AuthStatus, HomeSection, Playlist, SearchResults, Track } from "../types/music";
import type { PlayerState } from "../types/player";
import type { UpdateStatus } from "../types/update";

export const api = {
  authStatus: () => invoke<AuthStatus>("auth_status"),
  startWebLogin: () => invoke<void>("start_brave_login"),
  pollWebLogin: () => invoke<AuthStatus | null>("poll_brave_login"),
  configureAuth: (path: string) => invoke<AuthStatus>("configure_auth", { path }),
  clearAuth: () => invoke<AuthStatus>("clear_auth"),
  home: () => invoke<HomeSection[]>("get_home"),
  search: (query: string) => invoke<SearchResults>("search_music", { query }),
  libraryPlaylists: (limit = 100) => invoke<Playlist[]>("get_library_playlists", { limit }),
  libraryAlbums: (limit = 100) => invoke<Album[]>("get_library_albums", { limit }),
  libraryArtists: (limit = 100) => invoke<Artist[]>("get_library_artists", { limit }),
  likedSongs: (limit = 100) => invoke<Track[]>("get_liked_songs", { limit }),
  history: () => invoke<Track[]>("get_history"),
  playlistTracks: (playlistId: string) => invoke<Track[]>("get_playlist_tracks", { playlistId }),
  lyrics: (videoId: string) => invoke<string | null>("get_lyrics", { videoId }),
  player: () => invoke<PlayerState>("get_player_state"),
  queueTrack: (track: Track, playNow = true) => invoke<PlayerState>("queue_track", { track, playNow }),
  control: (action: string, value?: number) => invoke<PlayerState>("player_control", { action, value }),
  syncPlayback: (position: number, duration: number, isPlaying: boolean, volume: number) =>
    invoke<PlayerState>("sync_playback", { position, duration, isPlaying, volume }),
  playbackError: (message: string) => invoke<PlayerState>("playback_error", { message }),
  openMini: () => invoke<void>("open_mini_player"),
  checkForUpdates: () => invoke<UpdateStatus>("check_for_updates"),
  installUpdate: () => invoke<void>("install_update")
};
