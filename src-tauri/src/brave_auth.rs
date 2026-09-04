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
use rusqlite::{backup::Backup, Connection, OpenFlags};
use serde_json::{json, Value};
use tauri::{AppHandle, Manager, State};
use tokio_tungstenite::{connect_async, tungstenite::Message};

use crate::{models::AuthStatus, music_service::MusicServiceState};

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

fn brave_auth_root(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    let profile = dir.join("brave-auth-profile");
    fs::create_dir_all(&profile).map_err(|e| e.to_string())?;
    Ok(profile)
}

fn normal_brave_user_data() -> Result<PathBuf, String> {
    let local = env::var("LOCALAPPDATA")
        .map_err(|_| "LOCALAPPDATA is unavailable, so the Brave profile could not be located.".to_string())?;
    let root = Path::new(&local)
        .join("BraveSoftware")
        .join("Brave-Browser")
        .join("User Data");
    if root.is_dir() {
        Ok(root)
    } else {
        Err(format!("Your Brave profile was not found at {}.", root.display()))
    }
}

fn safe_profile_name(value: &str) -> Option<String> {
    let value = value.trim();
    if value.is_empty()
        || value == "."
        || value == ".."
        || value.contains('\\')
        || value.contains('/')
    {
        None
    } else {
        Some(value.to_string())
    }
}

fn profile_name_from_local_state(root: &Path) -> String {
    let path = root.join("Local State");
    let parsed = fs::read_to_string(path)
        .ok()
        .and_then(|raw| serde_json::from_str::<Value>(&raw).ok());

    parsed
        .as_ref()
        .and_then(|value| value.get("profile"))
        .and_then(|value| value.get("last_used"))
        .and_then(Value::as_str)
        .and_then(safe_profile_name)
        .unwrap_or_else(|| "Default".to_string())
}

fn copy_file_read_write(source: &Path, destination: &Path, required: bool) -> Result<(), String> {
    if !source.exists() {
        if required {
            return Err(format!("Required Brave profile file is missing: {}", source.display()));
        }
        return Ok(());
    }

    if let Some(parent) = destination.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }

    let mut last_error = None;
    for _ in 0..20 {
        match fs::read(source).and_then(|bytes| fs::write(destination, bytes)) {
            Ok(_) => return Ok(()),
            Err(error) => {
                last_error = Some(error);
                thread::sleep(Duration::from_millis(75));
            }
        }
    }

    if required {
        let error = last_error
            .map(|e| e.to_string())
            .unwrap_or_else(|| "unknown read error".to_string());
        Err(format!("Could not read Brave session metadata from {}: {error}", source.display()))
    } else {
        Ok(())
    }
}

fn remove_sqlite_sidecars(path: &Path) {
    let _ = fs::remove_file(path);
    let raw = path.to_string_lossy();
    let _ = fs::remove_file(PathBuf::from(format!("{raw}-wal")));
    let _ = fs::remove_file(PathBuf::from(format!("{raw}-shm")));
    let _ = fs::remove_file(PathBuf::from(format!("{raw}-journal")));
}

fn snapshot_sqlite_database(source: &Path, destination: &Path) -> Result<(), String> {
    if !source.is_file() {
        return Err(format!("Brave cookie database is missing: {}", source.display()));
    }

    if let Some(parent) = destination.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }
    remove_sqlite_sidecars(destination);

    // Opening SQLite read-only is different from copying the database file on
    // Windows. SQLite cooperates with Brave's WAL/locking protocol and the
    // online backup API takes a consistent snapshot while Brave remains open.
    let source_db = Connection::open_with_flags(source, OpenFlags::SQLITE_OPEN_READ_ONLY)
        .map_err(|e| format!("Could not open Brave's live cookie database: {e}"))?;
    source_db
        .busy_timeout(Duration::from_secs(8))
        .map_err(|e| format!("Could not configure Brave cookie snapshot: {e}"))?;

    let mut destination_db = Connection::open(destination)
        .map_err(|e| format!("Could not create YTM Desktop's temporary Brave cookie database: {e}"))?;

    let backup = Backup::new(&source_db, &mut destination_db)
        .map_err(|e| format!("Could not start the live Brave cookie snapshot: {e}"))?;
    backup
        .run_to_completion(128, Duration::from_millis(25), None)
        .map_err(|e| {
            format!(
                "Could not snapshot Brave's live cookie database while Brave is running: {e}. If Brave is updating or shutting down, wait a few seconds and try again."
            )
        })?;

    Ok(())
}

fn seed_auth_profile_from_existing_brave(auth_root: &Path) -> Result<String, String> {
    let source_root = normal_brave_user_data()?;
    let source_local_state = source_root.join("Local State");
    let mut profile_name = profile_name_from_local_state(&source_root);
    let mut source_profile = source_root.join(&profile_name);

    if !source_profile.is_dir() {
        profile_name = "Default".to_string();
        source_profile = source_root.join(&profile_name);
    }
    if !source_profile.is_dir() {
        return Err("No usable Brave profile was found. Open Brave once, then try again.".to_string());
    }

    fs::create_dir_all(auth_root).map_err(|e| e.to_string())?;
    for stale in ["DevToolsActivePort", "SingletonLock", "SingletonSocket", "SingletonCookie"] {
        let _ = fs::remove_file(auth_root.join(stale));
    }

    // Local State contains Brave's local encryption metadata. We copy it on the
    // same Windows account and still use Brave itself to decrypt the cookie data.
    copy_file_read_write(&source_local_state, &auth_root.join("Local State"), true)?;

    let destination_profile = auth_root.join(&profile_name);
    fs::create_dir_all(destination_profile.join("Network")).map_err(|e| e.to_string())?;

    snapshot_sqlite_database(
        &source_profile.join("Network").join("Cookies"),
        &destination_profile.join("Network").join("Cookies"),
    )?;

    // These are cosmetic/profile hints rather than authentication-critical.
    // If Brave is rewriting either file, do not block login on them.
    let _ = copy_file_read_write(
        &source_profile.join("Preferences"),
        &destination_profile.join("Preferences"),
        false,
    );
    let _ = copy_file_read_write(
        &source_profile.join("Secure Preferences"),
        &destination_profile.join("Secure Preferences"),
        false,
    );

    Ok(profile_name)
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

    Err("Brave Browser was not found. Install Brave, then try Sign in with Google again.".to_string())
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

fn launch_brave(exe: &Path, auth_root: &Path, profile_name: &str) -> Result<(), String> {
    Command::new(exe)
        .arg(format!("--user-data-dir={}", auth_root.display()))
        .arg(format!("--profile-directory={profile_name}"))
        .arg("--remote-debugging-address=127.0.0.1")
        .arg("--remote-debugging-port=0")
        .arg("--no-first-run")
        .arg("--no-default-browser-check")
        .arg("--disable-background-mode")
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
        Err("Brave opened, but YTM Desktop could not connect to the sign-in session. Close the YTM auth Brave window and try again.".to_string())
    })
    .await
    .map_err(|e| e.to_string())?
}

pub async fn start(app: &AppHandle) -> Result<(), String> {
    let exe = find_brave()?;
    let auth_root = brave_auth_root(app)?;

    if endpoint_alive(&auth_root).await {
        let profile_name = profile_name_from_local_state(&auth_root);
        launch_brave(&exe, &auth_root, &profile_name)?;
        return Ok(());
    }

    let profile_name = tauri::async_runtime::spawn_blocking({
        let auth_root = auth_root.clone();
        move || seed_auth_profile_from_existing_brave(&auth_root)
    })
    .await
    .map_err(|e| e.to_string())??;

    launch_brave(&exe, &auth_root, &profile_name)?;
    wait_for_devtools(auth_root).await
}

fn domain_matches_music_youtube(domain: &str) -> bool {
    let normalized = domain.trim_start_matches('.').to_ascii_lowercase();
    normalized == "music.youtube.com"
        || normalized == "youtube.com"
        || "music.youtube.com".ends_with(&format!(".{normalized}"))
}

async fn cdp_command(app: &AppHandle, method: &str) -> Result<Option<Value>, String> {
    let profile = brave_auth_root(app)?;
    let Some((port, browser_path)) = read_devtools_endpoint(&profile)? else {
        return Ok(None);
    };

    let ws_url = format!("ws://127.0.0.1:{port}{browser_path}");
    let (mut socket, _) = match connect_async(ws_url.as_str()).await {
        Ok(connection) => connection,
        Err(_) => return Ok(None),
    };

    let request = json!({"id": 1, "method": method});
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
