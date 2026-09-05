use std::{collections::BTreeMap, fs};
use tauri::{AppHandle, Emitter, Manager, State, WebviewUrl, WebviewWindowBuilder};

use crate::{
    models::*,
    music_service::{normalize_auth_path, MusicServiceState},
    official_web_player,
    player::{PlayerSnapshot, PlayerState},
    update_service::{self, UpdateStatus},
};

fn config_marker(app: &AppHandle) -> Result<std::path::PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    Ok(dir.join("auth-path.txt"))
}

fn managed_auth_path(app: &AppHandle) -> Result<std::path::PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    Ok(dir.join("ytm-session.json"))
}

#[tauri::command]
pub async fn start_web_login(app: AppHandle) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("ytm-login") {
        window.show().map_err(|e| e.to_string())?;
        window.set_focus().map_err(|e| e.to_string())?;
        return Ok(());
    }

    let url = tauri::Url::parse("https://music.youtube.com/").map_err(|e| e.to_string())?;
    WebviewWindowBuilder::new(&app, "ytm-login", WebviewUrl::External(url))
        .title("Sign in to YouTube Music")
        .inner_size(1050.0, 760.0)
        .min_inner_size(760.0, 540.0)
        .resizable(true)
        .center()
        .build()
        .map_err(|e| e.to_string())?;
    Ok(())
}

#[tauri::command]
pub async fn poll_web_login(
    app: AppHandle,
    music: State<'_, MusicServiceState>,
) -> Result<Option<AuthStatus>, String> {
    let window = match app.get_webview_window("ytm-login") {
        Some(window) => window,
        None => return Err("Sign-in window was closed before login completed.".into()),
    };

    let url = tauri::Url::parse("https://music.youtube.com/").map_err(|e| e.to_string())?;
    let cookies = tauri::async_runtime::spawn_blocking(move || {
        window.cookies_for_url(url).map_err(|e| e.to_string())
    })
    .await
    .map_err(|e| e.to_string())??;

    let mut jar = BTreeMap::<String, String>::new();
    for cookie in cookies {
        jar.insert(cookie.name().to_string(), cookie.value().to_string());
    }

    if !jar.contains_key("__Secure-3PAPISID") && !jar.contains_key("SAPISID") {
        return Ok(None);
    }

    let cookie_header = jar
        .into_iter()
        .map(|(name, value)| format!("{name}={value}"))
        .collect::<Vec<_>>()
        .join("; ");

    let path = managed_auth_path(&app)?;
    let mut last_error = String::new();

    for auth_user in 0..=5 {
        let raw = serde_json::to_string_pretty(&serde_json::json!({
            "Cookie": cookie_header.clone(),
            "Origin": "https://music.youtube.com",
            "X-Origin": "https://music.youtube.com",
            "Referer": "https://music.youtube.com/",
            "X-Goog-AuthUser": auth_user.to_string()
        }))
        .map_err(|e| e.to_string())?;

        match music.inner().configure_json(&raw, path.clone()).await {
            Ok(status) => {
                fs::write(&path, raw.as_bytes()).map_err(|e| e.to_string())?;
                fs::write(config_marker(&app)?, path.to_string_lossy().as_bytes())
                    .map_err(|e| e.to_string())?;

                if let Some(window) = app.get_webview_window("ytm-login") {
                    let _ = window.close();
                }
                return Ok(Some(status));
            }
            Err(error) => last_error = error,
        }
    }

    Err(format!(
        "Google sign-in cookies were found, but YouTube Music could not validate the session: {last_error}"
    ))
}

#[tauri::command]
pub async fn auth_status(music: State<'_, MusicServiceState>) -> Result<AuthStatus, String> {
    Ok(music.status().await)
}

#[tauri::command]
pub async fn configure_auth(
    app: AppHandle,
    music: State<'_, MusicServiceState>,
    path: String,
) -> Result<AuthStatus, String> {
    let path = normalize_auth_path(&path)?;
    let status = music.configure(path.clone()).await?;
    fs::write(config_marker(&app)?, path.to_string_lossy().as_bytes()).map_err(|e| e.to_string())?;
    official_web_player::reset(&app);
    Ok(status)
}

#[tauri::command]
pub async fn clear_auth(
    app: AppHandle,
    music: State<'_, MusicServiceState>,
) -> Result<AuthStatus, String> {
    music.clear().await;
    official_web_player::reset(&app);
    let _ = fs::remove_file(config_marker(&app)?);
    if let Ok(path) = managed_auth_path(&app) {
        let _ = fs::remove_file(path);
    }
    Ok(music.status().await)
}

#[tauri::command]
pub async fn get_home(music: State<'_, MusicServiceState>) -> Result<Vec<HomeSectionVm>, String> {
    music.home().await
}

#[tauri::command]
pub async fn search_music(
    music: State<'_, MusicServiceState>,
    query: String,
) -> Result<SearchResultsVm, String> {
    music.search(&query).await
}

#[tauri::command]
pub async fn get_library_playlists(
    music: State<'_, MusicServiceState>,
    limit: Option<usize>,
) -> Result<Vec<PlaylistVm>, String> {
    music.playlists(limit.unwrap_or(100)).await
}

#[tauri::command]
pub async fn get_library_albums(
    music: State<'_, MusicServiceState>,
    limit: Option<usize>,
) -> Result<Vec<AlbumVm>, String> {
    music.albums(limit.unwrap_or(100)).await
}

#[tauri::command]
pub async fn get_library_artists(
    music: State<'_, MusicServiceState>,
    limit: Option<usize>,
) -> Result<Vec<ArtistVm>, String> {
    music.artists(limit.unwrap_or(100)).await
}

#[tauri::command]
pub async fn get_liked_songs(
    music: State<'_, MusicServiceState>,
    limit: Option<usize>,
) -> Result<Vec<TrackVm>, String> {
    music.liked(limit.unwrap_or(100)).await
}

#[tauri::command]
pub async fn get_history(music: State<'_, MusicServiceState>) -> Result<Vec<TrackVm>, String> {
    music.history().await
}

#[tauri::command]
pub async fn get_playlist_tracks(
    music: State<'_, MusicServiceState>,
    playlist_id: String,
) -> Result<Vec<TrackVm>, String> {
    music.playlist_tracks(&playlist_id).await
}

#[tauri::command]
pub async fn get_lyrics(
    music: State<'_, MusicServiceState>,
    video_id: String,
) -> Result<Option<String>, String> {
    music.lyrics(&video_id).await
}

fn emit_player(app: &AppHandle, snapshot: &PlayerSnapshot) {
    let _ = app.emit("player-state", snapshot);
}

#[tauri::command]
pub async fn get_player_state(
    app: AppHandle,
    player: State<'_, PlayerState>,
) -> Result<PlayerSnapshot, String> {
    match official_web_player::status(&app).await {
        Ok(Some(media)) => Ok(player.sync_web(&media).await),
        Ok(None) | Err(_) => Ok(player.snapshot().await),
    }
}

#[tauri::command]
pub async fn queue_track(
    app: AppHandle,
    player: State<'_, PlayerState>,
    track: TrackVm,
    play_now: bool,
) -> Result<PlayerSnapshot, String> {
    let volume = player.snapshot().await.volume;
    if let Err(error) = official_web_player::load_track(&app, &track.video_id, play_now, volume).await {
        let snapshot = player.playback_error(error.clone()).await;
        emit_player(&app, &snapshot);
        return Err(error);
    }

    let snapshot = player.queue_track(track, play_now).await;
    emit_player(&app, &snapshot);
    Ok(snapshot)
}

#[tauri::command]
pub async fn player_control(
    app: AppHandle,
    player: State<'_, PlayerState>,
    action: String,
    value: Option<f64>,
) -> Result<PlayerSnapshot, String> {
    let snapshot = match action.as_str() {
        "next" | "previous" => {
            let direction = if action == "next" { 1 } else { -1 };
            if let Some((index, track, autoplay)) = player.adjacent_target(direction).await {
                let volume = player.snapshot().await.volume;
                official_web_player::load_track(&app, &track.video_id, autoplay, volume).await?;
                player.activate_index(index, track, autoplay).await
            } else {
                player.snapshot().await
            }
        }
        "play_pause" | "play" | "pause" => {
            official_web_player::control(&app, &action, None)?;
            player.control_simple(&action, None).await
        }
        "seek" => {
            let seconds = value.ok_or_else(|| "Seek requires a position.".to_string())?;
            official_web_player::control(&app, "seek", Some(seconds))?;
            player.control_simple("seek", Some(seconds)).await
        }
        "volume" => {
            let volume = value.ok_or_else(|| "Volume requires a value.".to_string())?;
            official_web_player::control(&app, "volume", Some(volume))?;
            player.control_simple("volume", Some(volume)).await
        }
        _ => player.control_simple(&action, value).await,
    };

    emit_player(&app, &snapshot);
    Ok(snapshot)
}

// Compatibility command for frontends from the pre-0.4 playback implementation.
#[tauri::command]
pub async fn sync_playback(
    app: AppHandle,
    player: State<'_, PlayerState>,
    position: f64,
    duration: f64,
    is_playing: bool,
    volume: f64,
) -> Result<PlayerSnapshot, String> {
    let snapshot = player
        .sync_playback(position, duration, is_playing, volume)
        .await;
    emit_player(&app, &snapshot);
    Ok(snapshot)
}

#[tauri::command]
pub async fn playback_error(
    app: AppHandle,
    player: State<'_, PlayerState>,
    message: String,
) -> Result<PlayerSnapshot, String> {
    let snapshot = player.playback_error(message).await;
    emit_player(&app, &snapshot);
    Ok(snapshot)
}

#[tauri::command]
pub async fn open_mini_player(app: AppHandle) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("mini") {
        window.show().map_err(|e| e.to_string())?;
        window.set_focus().map_err(|e| e.to_string())?;
    }
    Ok(())
}

#[tauri::command]
pub async fn check_for_updates(app: AppHandle) -> Result<UpdateStatus, String> {
    update_service::check(&app).await
}

#[tauri::command]
pub async fn install_update(app: AppHandle) -> Result<(), String> {
    update_service::install(&app).await
}
