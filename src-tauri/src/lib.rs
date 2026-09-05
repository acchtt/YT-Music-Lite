mod brave_auth;
mod commands;
mod models;
mod music_service;
mod native_audio;
mod playback_resolver;
mod player;
mod update_service;

use std::fs;
use tauri::Manager;

use brave_auth::BraveAuthBridgeState;
use music_service::MusicServiceState;
use native_audio::NativeAudioState;
use playback_resolver::PlaybackResolverState;
use player::PlayerState;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(MusicServiceState::default())
        .manage(PlaybackResolverState::default())
        .manage(NativeAudioState::default())
        .manage(PlayerState::default())
        .manage(BraveAuthBridgeState::default())
        .setup(|app| {
            let handle = app.handle().clone();

            if let Ok(dir) = handle.path().app_config_dir() {
                let marker = dir.join("auth-path.txt");
                if let Ok(path) = fs::read_to_string(marker) {
                    let state = handle.state::<MusicServiceState>();
                    tauri::async_runtime::block_on(async {
                        let _ = state.load_without_validation(path.trim().into()).await;
                    });
                }
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            commands::auth_status,
            brave_auth::start_brave_login,
            brave_auth::poll_brave_login,
            commands::start_web_login,
            commands::poll_web_login,
            commands::configure_auth,
            commands::clear_auth,
            commands::get_home,
            commands::search_music,
            commands::get_library_playlists,
            commands::get_library_albums,
            commands::get_library_artists,
            commands::get_liked_songs,
            commands::get_history,
            commands::get_playlist_tracks,
            commands::get_lyrics,
            commands::get_player_state,
            commands::queue_track,
            commands::player_control,
            commands::sync_playback,
            commands::playback_error,
            commands::open_mini_player,
            commands::check_for_updates,
            commands::install_update,
        ])
        .run(tauri::generate_context!())
        .expect("error while running YTM Desktop");
}
