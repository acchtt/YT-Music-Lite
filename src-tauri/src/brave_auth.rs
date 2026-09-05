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

const GOOGLE_ACCOUNT_CHOOSER_URL: &str =
    "https://accounts.google.com/AccountChooser?service=youtube&continue=https%3A%2F%2Fmusic.youtube.com%2F";

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

fn auth_browser_user_data(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    let profile = dir.join("brave-auth-browser");
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

    Err("Brave Browser was not found. Install Brave, then try Sign in with Google again.".to_string())
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
        "version": "1.3.0",
        "description": "Completes Google sign-in for YTM Desktop in its dedicated Brave authentication profile.",
        "permissions": ["cookies", "tabs"],
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

async function sendYouTubeSession(message, sender) {
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

    if (!response.ok) {
      return { captured: false, reason: `bridge-${response.status}` };
    }

    // This is a dedicated YTM Desktop Brave profile. Closing its only auth tab
    // after capture does not affect the user's normal Brave windows.
    if (sender && sender.tab && typeof sender.tab.id === "number") {
      setTimeout(() => chrome.tabs.remove(sender.tab.id).catch(() => {}), 900);
    }

    return { captured: true };
  } catch (_) {
    return { captured: false, reason: "bridge-unreachable" };
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || message.type !== "ytm-desktop-auto-capture-account") {
    return;
  }

  sendYouTubeSession(message, sender)
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
  window.setInterval(publishAccountContext, 500);
  window.addEventListener("yt-navigate-finish", publishAccountContext);
})();
"#;

    let content = r#"(() => {
  let accountContext = { authUser: null, pageId: null };
  let capturedKey = null;
  let sending = false;
  let retryTimer = null;

  function showStatus(text) {
    let toast = document.getElementById("ytm-desktop-signin-status");
    if (!toast) {
      toast = document.createElement("div");
      toast.id = "ytm-desktop-signin-status";
      toast.style.cssText = [
        "position:fixed",
        "right:20px",
        "bottom:20px",
        "z-index:2147483647",
        "padding:12px 15px",
        "border-radius:12px",
        "background:rgba(20,20,20,.96)",
        "color:#fff",
        "font:600 14px/1.4 system-ui,-apple-system,Segoe UI,sans-serif",
        "box-shadow:0 12px 40px rgba(0,0,0,.45)",
        "border:1px solid rgba(255,255,255,.14)"
      ].join(";");
      document.documentElement.appendChild(toast);
    }
    toast.textContent = text;
  }

  function scheduleCapture(delay = 150) {
    if (retryTimer !== null) {
      window.clearTimeout(retryTimer);
    }
    retryTimer = window.setTimeout(captureSelectedGoogleAccount, delay);
  }

  async function captureSelectedGoogleAccount() {
    if (sending || !accountContext.authUser) return;

    const key = `${accountContext.authUser}|${accountContext.pageId || ""}`;
    if (capturedKey === key) return;

    sending = true;
    showStatus("Connecting your selected Google account…");

    try {
      const response = await chrome.runtime.sendMessage({
        type: "ytm-desktop-auto-capture-account",
        authUser: accountContext.authUser,
        pageId: accountContext.pageId
      });

      if (response && response.captured) {
        capturedKey = key;
        showStatus("Connected. Returning to YTM Desktop…");
        return;
      }

      if (response && response.reason === "not-signed-in") {
        showStatus("Finishing Google sign-in…");
      } else {
        showStatus("Waiting for the YouTube Music session…");
      }
    } catch (_) {
      showStatus("Waiting for YTM Desktop…");
    } finally {
      sending = false;
    }

    scheduleCapture(1000);
  }

  window.addEventListener("message", (event) => {
    if (event.source !== window || !event.data || event.data.source !== "ytm-desktop-account-context") {
      return;
    }

    const next = {
      authUser: typeof event.data.authUser === "string" ? event.data.authUser : null,
      pageId: typeof event.data.pageId === "string" ? event.data.pageId : null
    };

    const changed = next.authUser !== accountContext.authUser || next.pageId !== accountContext.pageId;
    accountContext = next;

    if (changed && accountContext.authUser) {
      capturedKey = null;
      scheduleCapture();
    }
  });

  showStatus("Finishing YTM Desktop sign-in…");
  scheduleCapture(400);
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

fn launch_brave_auth_profile(
    exe: &Path,
    user_data: &Path,
    extension_dir: &Path,
) -> Result<(), String> {
    Command::new(exe)
        .arg(format!("--user-data-dir={}", user_data.display()))
        .arg("--profile-directory=Default")
        .arg(format!("--load-extension={}", extension_dir.display()))
        .arg("--no-first-run")
        .arg("--no-default-browser-check")
        .arg("--disable-background-mode")
        .arg("--new-window")
        .arg(GOOGLE_ACCOUNT_CHOOSER_URL)
        .spawn()
        .map_err(|e| format!("Could not start Brave: {e}"))?;

    Ok(())
}

async fn start(app: &AppHandle, bridge: BraveAuthBridgeState) -> Result<(), String> {
    let exe = find_brave()?;
    let user_data = auth_browser_user_data(app)?;

    let (port, token) = start_bridge_server(bridge).await?;
    let extension_dir = write_bridge_extension(app, port, &token)?;

    // This profile belongs only to YTM Desktop authentication, so it can run at
    // the same time as the user's everyday Brave profile with no file/profile lock.
    launch_brave_auth_profile(&exe, &user_data, &extension_dir)
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
            // Keep the captured session and retry on the next frontend poll. The
            // YouTube Music account can take a moment to become usable immediately
            // after Google's redirect even though the cookies are already present.
            eprintln!(
                "Google-selected YouTube Music account is not ready yet: authuser={} page_id={:?}: {}",
                captured.auth_user,
                captured.page_id,
                error
            );
            Ok(None)
        }
    }
}
