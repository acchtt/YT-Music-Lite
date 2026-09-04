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
use serde::Deserialize;
use serde_json::{json, Map, Value};
use sha2::{Digest, Sha256};
use tauri::{AppHandle, Manager, State as TauriState};
use tokio::sync::Mutex;

use crate::{models::AuthStatus, music_service::MusicServiceState};

const YTM_URL: &str = "https://music.youtube.com/";

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CapturedSession {
    cookie_header: String,
    auth_user: String,
    page_id: Option<String>,
}

#[derive(Clone, Default)]
pub struct BraveAuthBridgeState {
    captured_session: Arc<Mutex<Option<CapturedSession>>>,
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
        .and_then(|raw| serde_json::from_str::<Value>(&raw).ok());

    parsed
        .as_ref()
        .and_then(|value| value.get("profile"))
        .and_then(|value| value.get("last_used"))
        .and_then(Value::as_str)
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

async fn capture_session(
    AxumPath(token): AxumPath<String>,
    AxumState(state): AxumState<BridgeServerState>,
    body: Bytes,
) -> StatusCode {
    if token != state.token {
        return StatusCode::FORBIDDEN;
    }

    if body.len() > 160 * 1024 {
        return StatusCode::PAYLOAD_TOO_LARGE;
    }

    let mut session: CapturedSession = match serde_json::from_slice(&body) {
        Ok(session) => session,
        Err(_) => return StatusCode::BAD_REQUEST,
    };

    if session.cookie_header.len() > 128 * 1024
        || (!session.cookie_header.contains("__Secure-3PAPISID=")
            && !session.cookie_header.contains("SAPISID="))
    {
        return StatusCode::BAD_REQUEST;
    }

    let auth_user = session.auth_user.trim();
    let parsed_auth_user = auth_user.parse::<u16>().ok();
    if parsed_auth_user.is_none() || parsed_auth_user.is_some_and(|value| value > 100) {
        return StatusCode::BAD_REQUEST;
    }
    session.auth_user = auth_user.to_string();

    session.page_id = session
        .page_id
        .take()
        .map(|value| value.trim().to_string())
        .filter(|value| !value.is_empty() && value.len() <= 512);

    *state.bridge.captured_session.lock().await = Some(session);
    StatusCode::NO_CONTENT
}

async fn start_bridge_server(bridge: BraveAuthBridgeState) -> Result<(u16, String), String> {
    *bridge.captured_session.lock().await = None;

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
        .route("/capture/{token}", post(capture_session))
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
        "version": "1.1.0",
        "description": "Transfers only the YouTube Music account explicitly selected by the user to YTM Desktop on this PC.",
        "permissions": ["cookies"],
        "host_permissions": [
            "https://music.youtube.com/*",
            "https://*.youtube.com/*",
            "http://127.0.0.1/*"
        ],
        "background": {
            "service_worker": "background.js"
        },
        "content_scripts": [
            {
                "matches": ["https://music.youtube.com/*"],
                "js": ["account-context.js"],
                "run_at": "document_idle",
                "world": "MAIN"
            },
            {
                "matches": ["https://music.youtube.com/*"],
                "js": ["content.js"],
                "run_at": "document_idle"
            }
        ]
    });

    fs::write(
        dir.join("manifest.json"),
        serde_json::to_vec_pretty(&manifest).map_err(|e| e.to_string())?,
    )
    .map_err(|e| format!("Could not write the Brave bridge manifest: {e}"))?;

    let endpoint = format!("http://127.0.0.1:{port}/capture/{token}");
    let endpoint_js = serde_json::to_string(&endpoint).map_err(|e| e.to_string())?;

    let background_template = r#"const ENDPOINT = __ENDPOINT__;

async function sendYouTubeSession(message) {
  const authUser = typeof message.authUser === "string" ? message.authUser.trim() : "";
  if (!/^\d+$/.test(authUser)) {
    return { captured: false, reason: "account-index-missing" };
  }

  const cookies = await chrome.cookies.getAll({ url: "https://music.youtube.com/" });
  const authenticated = cookies.some(
    (cookie) => cookie.name === "__Secure-3PAPISID" || cookie.name === "SAPISID"
  );

  if (!authenticated) {
    return { captured: false, reason: "not-signed-in" };
  }

  const cookieHeader = cookies
    .map((cookie) => `${cookie.name}=${cookie.value}`)
    .join("; ");

  const payload = {
    cookieHeader,
    authUser,
    pageId: typeof message.pageId === "string" && message.pageId.trim()
      ? message.pageId.trim()
      : null
  };

  try {
    const response = await fetch(ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    return response.ok
      ? { captured: true }
      : { captured: false, reason: `bridge-${response.status}` };
  } catch (_) {
    return { captured: false, reason: "bridge-unreachable" };
  }
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (!message || message.type !== "ytm-desktop-capture-selected-account") {
    return;
  }

  sendYouTubeSession(message)
    .then(sendResponse)
    .catch(() => sendResponse({ captured: false, reason: "unexpected-error" }));
  return true;
});
"#;
    let background = background_template.replace("__ENDPOINT__", &endpoint_js);

    let account_context = r#"(() => {
  function readConfig(name) {
    try {
      if (window.ytcfg && typeof window.ytcfg.get === "function") {
        const value = window.ytcfg.get(name);
        if (value !== undefined && value !== null && value !== "") {
          return String(value);
        }
      }
    } catch (_) {}

    try {
      const value = window.ytcfg && window.ytcfg.data_ ? window.ytcfg.data_[name] : null;
      if (value !== undefined && value !== null && value !== "") {
        return String(value);
      }
    } catch (_) {}

    return null;
  }

  function publishAccountContext() {
    window.postMessage({
      source: "ytm-desktop-account-context",
      authUser: readConfig("SESSION_INDEX"),
      pageId: readConfig("DELEGATED_SESSION_ID")
    }, "*");
  }

  publishAccountContext();
  window.setInterval(publishAccountContext, 750);
  window.addEventListener("yt-navigate-finish", publishAccountContext);
})();
"#;

    let content = r#"(() => {
  let accountContext = { authUser: null, pageId: null };

  window.addEventListener("message", (event) => {
    if (event.source !== window || !event.data || event.data.source !== "ytm-desktop-account-context") {
      return;
    }

    accountContext = {
      authUser: typeof event.data.authUser === "string" ? event.data.authUser : null,
      pageId: typeof event.data.pageId === "string" ? event.data.pageId : null
    };
    refreshButtonState();
  });

  function refreshButtonState() {
    const button = document.getElementById("ytm-desktop-connect-account");
    const hint = document.getElementById("ytm-desktop-account-hint");
    if (!button || !hint) return;

    const ready = !!accountContext.authUser;
    button.disabled = !ready;
    button.textContent = ready ? "Connect this account" : "Detecting active account…";
    hint.textContent = ready
      ? "Switch accounts in YouTube Music if needed. When the correct account is visible, click below."
      : "Waiting for YouTube Music to expose the active account…";
  }

  async function connectSelectedAccount() {
    const button = document.getElementById("ytm-desktop-connect-account");
    const hint = document.getElementById("ytm-desktop-account-hint");
    if (!button || !hint || !accountContext.authUser) return;

    button.disabled = true;
    button.textContent = "Connecting…";

    try {
      const response = await chrome.runtime.sendMessage({
        type: "ytm-desktop-capture-selected-account",
        authUser: accountContext.authUser,
        pageId: accountContext.pageId
      });

      if (response && response.captured) {
        hint.textContent = "Session sent to YTM Desktop. This Brave window can stay open.";
        button.textContent = "Sent";
        return;
      }

      hint.textContent = response && response.reason === "not-signed-in"
        ? "This YouTube Music page is not signed in yet. Sign in or switch account, then try again."
        : "Could not send this account to YTM Desktop. Try again.";
    } catch (_) {
      hint.textContent = "Could not reach YTM Desktop. Return to the app and start sign-in again.";
    }

    button.disabled = false;
    button.textContent = "Connect this account";
  }

  function installPanel() {
    if (document.getElementById("ytm-desktop-account-panel")) return;

    const panel = document.createElement("div");
    panel.id = "ytm-desktop-account-panel";
    panel.style.cssText = [
      "position:fixed",
      "right:20px",
      "bottom:20px",
      "z-index:2147483647",
      "width:320px",
      "padding:16px",
      "border-radius:14px",
      "background:rgba(20,20,20,.96)",
      "color:#fff",
      "font:14px/1.4 system-ui,-apple-system,Segoe UI,sans-serif",
      "box-shadow:0 12px 40px rgba(0,0,0,.45)",
      "border:1px solid rgba(255,255,255,.14)"
    ].join(";");

    const title = document.createElement("div");
    title.textContent = "YTM Desktop sign-in";
    title.style.cssText = "font-weight:700;font-size:15px;margin-bottom:6px";

    const hint = document.createElement("div");
    hint.id = "ytm-desktop-account-hint";
    hint.style.cssText = "opacity:.78;margin-bottom:12px";

    const button = document.createElement("button");
    button.id = "ytm-desktop-connect-account";
    button.type = "button";
    button.style.cssText = [
      "width:100%",
      "border:0",
      "border-radius:10px",
      "padding:10px 12px",
      "font:600 14px system-ui,-apple-system,Segoe UI,sans-serif",
      "cursor:pointer",
      "background:#fff",
      "color:#111"
    ].join(";");
    button.addEventListener("click", connectSelectedAccount);

    panel.append(title, hint, button);
    document.documentElement.appendChild(panel);
    refreshButtonState();
  }

  installPanel();
  window.setInterval(installPanel, 1500);
})();
"#;

    fs::write(dir.join("background.js"), background)
        .map_err(|e| format!("Could not write the Brave bridge background script: {e}"))?;
    fs::write(dir.join("account-context.js"), account_context)
        .map_err(|e| format!("Could not write the Brave account-context script: {e}"))?;
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
            "Close Brave completely before starting Google sign-in, then click Sign in with Google again. YTM Desktop opens your real Brave profile and loads a temporary local account-selection bridge."
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
    let captured = bridge.captured_session.lock().await.clone();
    let Some(captured) = captured else {
        return Ok(None);
    };

    let path = managed_auth_path(&app)?;
    let mut auth_json = Map::<String, Value>::new();
    auth_json.insert("Cookie".into(), Value::String(captured.cookie_header.clone()));
    auth_json.insert("Origin".into(), Value::String("https://music.youtube.com".into()));
    auth_json.insert("X-Origin".into(), Value::String("https://music.youtube.com".into()));
    auth_json.insert("Referer".into(), Value::String("https://music.youtube.com/".into()));
    auth_json.insert("X-Goog-AuthUser".into(), Value::String(captured.auth_user.clone()));

    if let Some(page_id) = captured.page_id.as_ref() {
        auth_json.insert("X-Goog-PageId".into(), Value::String(page_id.clone()));
    }

    let raw = serde_json::to_string_pretty(&Value::Object(auth_json)).map_err(|e| e.to_string())?;

    match music.inner().configure_json(&raw, path.clone()).await {
        Ok(status) => {
            fs::write(&path, raw.as_bytes()).map_err(|e| e.to_string())?;
            fs::write(config_marker(&app)?, path.to_string_lossy().as_bytes())
                .map_err(|e| e.to_string())?;
            *bridge.captured_session.lock().await = None;
            Ok(Some(status))
        }
        Err(error) => {
            eprintln!(
                "Selected Brave/YouTube Music account was not ready: authuser={} page_id={:?}: {}",
                captured.auth_user,
                captured.page_id,
                error
            );
            *bridge.captured_session.lock().await = None;
            Ok(None)
        }
    }
}
