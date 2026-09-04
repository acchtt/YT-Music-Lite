use std::{
    env,
    fs,
    path::{Path, PathBuf},
    process::Command,
    sync::Arc,
    time::{SystemTime, UNIX_EPOCH},
};

use axum::{
    body::Bytes,
    extract::{Path as AxumPath, State as AxumState},
    http::StatusCode,
    routing::post,
    Router,
};
use serde_json::json;
use sha2::{Digest, Sha256};
use tauri::{AppHandle, Manager, State as TauriState};
use tokio::sync::Mutex;

use crate::{models::AuthStatus, music_service::MusicServiceState};

const YTM_URL: &str = "https://music.youtube.com/";

#[derive(Clone, Default)]
pub struct BraveAuthBridgeState {
    captured_cookie: Arc<Mutex<Option<String>>>,
}

#[derive(Clone)]
struct BridgeServerState {
    token: String,
    bridge: BraveAuthBridgeState,
}

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

fn bridge_extension_dir(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    let extension = dir.join("brave-signin-bridge");
    fs::create_dir_all(&extension).map_err(|e| e.to_string())?;
    Ok(extension)
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
        .and_then(|raw| serde_json::from_str::<serde_json::Value>(&raw).ok());

    parsed
        .as_ref()
        .and_then(|value| value.get("profile"))
        .and_then(|value| value.get("last_used"))
        .and_then(serde_json::Value::as_str)
        .and_then(safe_profile_name)
        .unwrap_or_else(|| "Default".to_string())
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

#[cfg(target_os = "windows")]
fn brave_is_running() -> bool {
    let output = Command::new("tasklist")
        .args(["/FI", "IMAGENAME eq brave.exe", "/FO", "CSV", "/NH"])
        .output();

    match output {
        Ok(output) => String::from_utf8_lossy(&output.stdout)
            .to_ascii_lowercase()
            .contains("\"brave.exe\""),
        Err(_) => false,
    }
}

#[cfg(not(target_os = "windows"))]
fn brave_is_running() -> bool {
    false
}

fn bridge_token() -> String {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    let mut hasher = Sha256::new();
    hasher.update(std::process::id().to_le_bytes());
    hasher.update(now.to_le_bytes());
    format!("{:x}", hasher.finalize())
}

async fn capture_cookie(
    AxumPath(token): AxumPath<String>,
    AxumState(state): AxumState<BridgeServerState>,
    body: Bytes,
) -> StatusCode {
    if token != state.token {
        return StatusCode::FORBIDDEN;
    }

    let cookie = match String::from_utf8(body.to_vec()) {
        Ok(cookie) => cookie,
        Err(_) => return StatusCode::BAD_REQUEST,
    };

    if cookie.len() > 128 * 1024
        || (!cookie.contains("__Secure-3PAPISID=") && !cookie.contains("SAPISID="))
    {
        return StatusCode::BAD_REQUEST;
    }

    *state.bridge.captured_cookie.lock().await = Some(cookie);
    StatusCode::NO_CONTENT
}

async fn start_bridge_server(bridge: BraveAuthBridgeState) -> Result<(u16, String), String> {
    *bridge.captured_cookie.lock().await = None;

    let listener = tokio::net::TcpListener::bind(("127.0.0.1", 0))
        .await
        .map_err(|e| format!("Could not start the local Brave sign-in bridge: {e}"))?;
    let port = listener
        .local_addr()
        .map_err(|e| format!("Could not read the Brave sign-in bridge port: {e}"))?
        .port();

    let token = bridge_token();
    let state = BridgeServerState {
        token: token.clone(),
        bridge,
    };

    let router = Router::new()
        .route("/capture/{token}", post(capture_cookie))
        .with_state(state);

    tauri::async_runtime::spawn(async move {
        if let Err(error) = axum::serve(listener, router).await {
            eprintln!("Brave sign-in bridge stopped: {error}");
        }
    });

    Ok((port, token))
}

fn write_bridge_extension(app: &AppHandle, port: u16, token: &str) -> Result<PathBuf, String> {
    let dir = bridge_extension_dir(app)?;

    let manifest = json!({
        "manifest_version": 3,
        "name": "YTM Desktop Sign-in Bridge",
        "version": "1.0.0",
        "description": "Transfers only the active YouTube Music browser session to YTM Desktop on this PC.",
        "permissions": ["cookies"],
        "host_permissions": [
            "https://music.youtube.com/*",
            "https://*.youtube.com/*",
            "http://127.0.0.1/*"
        ],
        "background": {
            "service_worker": "background.js"
        },
        "content_scripts": [{
            "matches": ["https://music.youtube.com/*"],
            "js": ["content.js"],
            "run_at": "document_idle"
        }]
    });

    fs::write(
        dir.join("manifest.json"),
        serde_json::to_vec_pretty(&manifest).map_err(|e| e.to_string())?,
    )
    .map_err(|e| format!("Could not write the Brave bridge manifest: {e}"))?;

    let endpoint = format!("http://127.0.0.1:{port}/capture/{token}");
    let endpoint_js = serde_json::to_string(&endpoint).map_err(|e| e.to_string())?;

    let background = format!(
        r#"const ENDPOINT = {endpoint_js};

async function sendYouTubeSession() {{
  const cookies = await chrome.cookies.getAll({{ url: "https://music.youtube.com/" }});
  const authenticated = cookies.some(
    (cookie) => cookie.name === "__Secure-3PAPISID" || cookie.name === "SAPISID"
  );

  if (!authenticated) {{
    return false;
  }}

  const cookieHeader = cookies
    .map((cookie) => `${{cookie.name}}=${{cookie.value}}`)
    .join("; ");

  try {{
    await fetch(ENDPOINT, {{
      method: "POST",
      headers: {{ "Content-Type": "text/plain;charset=UTF-8" }},
      body: cookieHeader
    }});
  }} catch (_) {{
    // The local bridge may close after capture. The POST itself is what matters.
  }}

  return true;
}}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {{
  if (!message || message.type !== "ytm-desktop-capture") {{
    return;
  }}

  sendYouTubeSession()
    .then((captured) => sendResponse({{ captured }}))
    .catch(() => sendResponse({{ captured: false }}));
  return true;
}});
"#
    );

    let content = r#"function captureYtmSession() {
  chrome.runtime.sendMessage({ type: "ytm-desktop-capture" }).catch(() => {});
}

captureYtmSession();
window.setInterval(captureYtmSession, 1500);
"#;

    fs::write(dir.join("background.js"), background)
        .map_err(|e| format!("Could not write the Brave bridge background script: {e}"))?;
    fs::write(dir.join("content.js"), content)
        .map_err(|e| format!("Could not write the Brave bridge content script: {e}"))?;

    Ok(dir)
}

fn launch_brave_with_existing_profile(
    exe: &Path,
    profile_name: &str,
    extension_dir: &Path,
) -> Result<(), String> {
    Command::new(exe)
        .arg(format!("--profile-directory={profile_name}"))
        .arg(format!("--load-extension={}", extension_dir.display()))
        .arg("--no-first-run")
        .arg("--no-default-browser-check")
        .arg("--new-window")
        .arg(YTM_URL)
        .spawn()
        .map_err(|e| format!("Could not start Brave: {e}"))?;

    Ok(())
}

async fn start(app: &AppHandle, bridge: BraveAuthBridgeState) -> Result<(), String> {
    let exe = find_brave()?;

    if brave_is_running() {
        return Err(
            "Close Brave completely before starting Google sign-in, then click Sign in with Google again. YTM Desktop now opens your REAL Brave profile so your existing Google accounts are available. Brave must be closed briefly so the temporary sign-in bridge can load into that profile; after YTM Desktop connects, you can use Brave normally."
                .to_string(),
        );
    }

    let user_data = normal_brave_user_data()?;
    let profile_name = profile_name_from_local_state(&user_data);
    let profile_dir = user_data.join(&profile_name);

    if !profile_dir.is_dir() {
        return Err(format!(
            "Brave profile '{}' was not found at {}.",
            profile_name,
            profile_dir.display()
        ));
    }

    let (port, token) = start_bridge_server(bridge).await?;
    let extension_dir = write_bridge_extension(app, port, &token)?;
    launch_brave_with_existing_profile(&exe, &profile_name, &extension_dir)
}

#[tauri::command]
pub async fn start_brave_login(
    app: AppHandle,
    bridge: TauriState<'_, BraveAuthBridgeState>,
) -> Result<(), String> {
    start(&app, bridge.inner().clone()).await
}

#[tauri::command]
pub async fn poll_brave_login(
    app: AppHandle,
    music: TauriState<'_, MusicServiceState>,
    bridge: TauriState<'_, BraveAuthBridgeState>,
) -> Result<Option<AuthStatus>, String> {
    let cookie_header = bridge.captured_cookie.lock().await.clone();
    let Some(cookie_header) = cookie_header else {
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
                *bridge.captured_cookie.lock().await = None;
                return Ok(Some(status));
            }
            Err(error) => {
                eprintln!("Brave auth session not ready for authuser={auth_user}: {error}");
            }
        }
    }

    Ok(None)
}
