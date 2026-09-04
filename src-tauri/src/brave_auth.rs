use std::{
    collections::BTreeMap,
    env,
    fs,
    path::{Path, PathBuf},
    process::Command,
    thread,
    time::{Duration, Instant},
};

use futures_util::{SinkExt, StreamExt};
use serde_json::{json, Value};
use tauri::{AppHandle, Manager, State};
use tokio_tungstenite::{connect_async, tungstenite::Message};

use crate::{
    models::AuthStatus,
    music_service::MusicServiceState,
};

const YTM_URL: &str = "https://music.youtube.com/";

fn config_marker(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    Ok(dir.join("auth-path.txt"))
}

fn managed_auth_path(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    Ok(dir.join("ytm-session.json"))
}

fn brave_profile_dir(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    let profile = dir.join("brave-auth-profile");
    fs::create_dir_all(&profile).map_err(|e| e.to_string())?;
    Ok(profile)
}

fn find_brave() -> Result<PathBuf, String> {
    let mut candidates = Vec::<PathBuf>::new();

    if let Ok(local) = env::var("LOCALAPPDATA") {
        candidates.push(
            Path::new(&local)
                .join("BraveSoftware")
                .join("Brave-Browser")
                .join("Application")
                .join("brave.exe"),
        );
    }
    if let Ok(program_files) = env::var("PROGRAMFILES") {
        candidates.push(
            Path::new(&program_files)
                .join("BraveSoftware")
                .join("Brave-Browser")
                .join("Application")
                .join("brave.exe"),
        );
    }
    if let Ok(program_files_x86) = env::var("PROGRAMFILES(X86)") {
        candidates.push(
            Path::new(&program_files_x86)
                .join("BraveSoftware")
                .join("Brave-Browser")
                .join("Application")
                .join("brave.exe"),
        );
    }

    if let Some(found) = candidates.into_iter().find(|p| p.is_file()) {
        return Ok(found);
    }

    if let Ok(output) = Command::new("where.exe").arg("brave.exe").output() {
        if output.status.success() {
            if let Some(line) = String::from_utf8_lossy(&output.stdout)
                .lines()
                .map(str::trim)
                .find(|line| !line.is_empty())
            {
                let path = PathBuf::from(line);
                if path.is_file() {
                    return Ok(path);
                }
            }
        }
    }

    Err(
        "Brave Browser was not found. Install Brave, then try Sign in with Google again."
            .to_string(),
    )
}

fn read_devtools_endpoint(profile: &Path) -> Result<Option<(u16, String)>, String> {
    let active_port = profile.join("DevToolsActivePort");
    let raw = match fs::read_to_string(active_port) {
        Ok(raw) => raw,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(e) => return Err(e.to_string()),
    };

    let mut lines = raw.lines();
    let port = match lines.next().and_then(|v| v.trim().parse::<u16>().ok()) {
        Some(port) => port,
        None => return Ok(None),
    };
    let path = match lines.next().map(str::trim).filter(|v| !v.is_empty()) {
        Some(path) => path.to_string(),
        None => return Ok(None),
    };

    Ok(Some((port, path)))
}

async fn endpoint_alive(profile: &Path) -> bool {
    let Some((port, _)) = read_devtools_endpoint(profile).ok().flatten() else {
        return false;
    };

    let client = match reqwest::Client::builder()
        .timeout(Duration::from_secs(1))
        .build()
    {
        Ok(client) => client,
        Err(_) => return false,
    };

    client
        .get(format!("http://127.0.0.1:{port}/json/version"))
        .send()
        .await
        .map(|r| r.status().is_success())
        .unwrap_or(false)
}

fn launch_brave(exe: &Path, profile: &Path) -> Result<(), String> {
    Command::new(exe)
        .arg(format!("--user-data-dir={}", profile.display()))
        .arg("--remote-debugging-address=127.0.0.1")
        .arg("--remote-debugging-port=0")
        .arg("--no-first-run")
        .arg("--no-default-browser-check")
        .arg("--new-window")
        .arg(YTM_URL)
        .spawn()
        .map_err(|e| format!("Could not start Brave: {e}"))?;

    Ok(())
}

async fn wait_for_devtools(profile: PathBuf) -> Result<(), String> {
    tauri::async_runtime::spawn_blocking(move || {
        let started = Instant::now();
        while started.elapsed() < Duration::from_secs(12) {
            if read_devtools_endpoint(&profile)?.is_some() {
                return Ok(());
            }
            thread::sleep(Duration::from_millis(150));
        }
        Err(
            "Brave opened, but YTM Desktop could not connect to the sign-in session. Close the Brave auth window and try again."
                .to_string(),
        )
    })
    .await
    .map_err(|e| e.to_string())?
}

pub async fn start(app: &AppHandle) -> Result<(), String> {
    let exe = find_brave()?;
    let profile = brave_profile_dir(app)?;

    if endpoint_alive(&profile).await {
        launch_brave(&exe, &profile)?;
        return Ok(());
    }

    let _ = fs::remove_file(profile.join("DevToolsActivePort"));

    launch_brave(&exe, &profile)?;
    wait_for_devtools(profile).await
}

fn domain_matches_music_youtube(domain: &str) -> bool {
    let normalized = domain.trim_start_matches('.').to_ascii_lowercase();
    normalized == "music.youtube.com"
        || normalized == "youtube.com"
        || "music.youtube.com".ends_with(&format!(".{normalized}"))
}

async fn cdp_command(app: &AppHandle, method: &str) -> Result<Option<Value>, String> {
    let profile = brave_profile_dir(app)?;
    let Some((port, browser_path)) = read_devtools_endpoint(&profile)? else {
        return Ok(None);
    };

    let ws_url = format!("ws://127.0.0.1:{port}{browser_path}");
    let (mut socket, _) = match connect_async(ws_url.as_str()).await {
        Ok(connection) => connection,
        Err(_) => return Ok(None),
    };

    let request = json!({
        "id": 1,
        "method": method
    });

    socket
        .send(Message::Text(request.to_string().into()))
        .await
        .map_err(|e| e.to_string())?;

    while let Some(message) = socket.next().await {
        let message = message.map_err(|e| e.to_string())?;
        if !message.is_text() {
            continue;
        }

        let text = message.into_text().map_err(|e| e.to_string())?.to_string();
        let value: Value = serde_json::from_str(&text).map_err(|e| e.to_string())?;

        if value.get("id").and_then(Value::as_i64) == Some(1) {
            return Ok(Some(value));
        }
    }

    Ok(None)
}

async fn music_cookie_header(app: &AppHandle) -> Result<Option<String>, String> {
    let Some(response) = cdp_command(app, "Storage.getCookies").await? else {
        return Ok(None);
    };

    let Some(cookies) = response
        .get("result")
        .and_then(|v| v.get("cookies"))
        .and_then(Value::as_array)
    else {
        return Ok(None);
    };

    let mut jar = BTreeMap::<String, String>::new();

    for cookie in cookies {
        let Some(name) = cookie.get("name").and_then(Value::as_str) else {
            continue;
        };
        let Some(value) = cookie.get("value").and_then(Value::as_str) else {
            continue;
        };
        let Some(domain) = cookie.get("domain").and_then(Value::as_str) else {
            continue;
        };

        if domain_matches_music_youtube(domain) {
            jar.insert(name.to_string(), value.to_string());
        }
    }

    if !jar.contains_key("__Secure-3PAPISID") && !jar.contains_key("SAPISID") {
        return Ok(None);
    }

    Ok(Some(
        jar.into_iter()
            .map(|(name, value)| format!("{name}={value}"))
            .collect::<Vec<_>>()
            .join("; "),
    ))
}

async fn close_auth_browser(app: &AppHandle) {
    let _ = cdp_command(app, "Browser.close").await;
}

#[tauri::command]
pub async fn start_brave_login(app: AppHandle) -> Result<(), String> {
    start(&app).await
}

#[tauri::command]
pub async fn poll_brave_login(
    app: AppHandle,
    music: State<'_, MusicServiceState>,
) -> Result<Option<AuthStatus>, String> {
    let Some(cookie_header) = music_cookie_header(&app).await? else {
        return Ok(None);
    };

    let path = managed_auth_path(&app)?;

    for auth_user in 0..=5 {
        let raw = serde_json::to_string_pretty(&json!({
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

                close_auth_browser(&app).await;
                return Ok(Some(status));
            }
            Err(error) => {
                eprintln!("Brave auth session not ready for authuser={auth_user}: {error}");
            }
        }
    }

    Ok(None)
}
