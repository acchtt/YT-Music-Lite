mod brave_auth;
mod commands;
mod discovery;
mod models;
mod music_service;
mod update_service;

use std::fs;
use tauri::Manager;

use brave_auth::BraveAuthBridgeState;
use discovery::DiscoveryState;
use music_service::MusicServiceState;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(MusicServiceState::default())
        .manage(BraveAuthBridgeState::default())
        .setup(|app| {
            let handle = app.handle().clone();
            let discovery = DiscoveryState::new(&handle).map_err(std::io::Error::other)?;
            app.manage(discovery);

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
            commands::record_listen,
            commands::get_discover_weekly,
            commands::check_for_updates,
            commands::install_update,
        ])
        .run(tauri::generate_context!())
        .expect("error while running YTM Desktop");
}
