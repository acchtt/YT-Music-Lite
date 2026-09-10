use std::{collections::BTreeMap, fs};
use tauri::{AppHandle, Manager, State, WebviewUrl, WebviewWindowBuilder};

use crate::{
    discovery::DiscoveryState,
    models::*,
    music_service::{normalize_auth_path, MusicServiceState},
    update_service::{self, UpdateStatus},
};

#[tauri::command]
pub async fn record_listen(
    discovery: State<'_, DiscoveryState>,
    track: TrackVm,
    event_type: String,
) -> Result<(), String> {
    discovery.record(&track, &event_type)
}

#[tauri::command]
pub async fn get_discover_weekly(
    music: State<'_, MusicServiceState>,
    discovery: State<'_, DiscoveryState>,
    limit: Option<usize>,
) -> Result<Vec<TrackVm>, String> {
    let limit = limit.unwrap_or(30).clamp(1, 50);
    let artists = discovery.top_artists(6)?;
    if artists.is_empty() {
        return discovery.ranked_tracks(limit);
    }

    let seen = discovery.seen_ids()?;
    let mut added = std::collections::HashSet::new();
    let mut result = Vec::with_capacity(limit);

    for artist in artists {
        let search = match music.search(&artist).await {
            Ok(value) => value,
            Err(_) => continue,
        };
        let mut artist_count = 0;
        for track in search.tracks {
            if seen.contains(&track.video_id) || !added.insert(track.video_id.clone()) {
                continue;
            }
            result.push(track);
            artist_count += 1;
            if artist_count == 3 || result.len() == limit {
                break;
            }
        }
        if result.len() == limit {
            break;
        }
    }

    if result.len() < limit {
        for track in discovery.ranked_tracks(limit - result.len())? {
            if added.insert(track.video_id.clone()) {
                result.push(track);
            }
        }
    }
    Ok(result)
}

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
    fs::write(config_marker(&app)?, path.to_string_lossy().as_bytes())
        .map_err(|e| e.to_string())?;
    Ok(status)
}

#[tauri::command]
pub async fn clear_auth(
    app: AppHandle,
    music: State<'_, MusicServiceState>,
) -> Result<AuthStatus, String> {
    music.clear().await;
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

#[tauri::command]
pub async fn check_for_updates(app: AppHandle) -> Result<UpdateStatus, String> {
    update_service::check(&app).await
}

#[tauri::command]
pub async fn install_update(app: AppHandle) -> Result<(), String> {
    update_service::install(&app).await
}
