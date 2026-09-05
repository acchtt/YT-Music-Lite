use std::{fs, path::PathBuf, time::Duration};

use serde::Deserialize;
use serde_json::Value;
use tauri::{
    webview::Cookie, AppHandle, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder,
};
use tokio::sync::oneshot;

const PLAYER_LABEL: &str = "ytm-official-player";
const PLAYER_HOME: &str = "https://music.youtube.com/";

#[derive(Debug, Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WebMediaStatus {
    pub ready: bool,
    pub position: f64,
    pub duration: f64,
    pub is_playing: bool,
    pub volume: f64,
    pub ended: bool,
    pub page_url: String,
    pub title: String,
    pub error: Option<String>,
}

fn auth_source_path(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app.path().app_config_dir().map_err(|e| e.to_string())?;
    let marker = dir.join("auth-path.txt");
    if let Ok(path) = fs::read_to_string(&marker) {
        let path = PathBuf::from(path.trim());
        if path.is_file() {
            return Ok(path);
        }
    }

    let managed = dir.join("ytm-session.json");
    if managed.is_file() {
        return Ok(managed);
    }

    Err("YouTube Music is not signed in yet. Sign in once, then try playback again.".into())
}

fn read_cookie_header(app: &AppHandle) -> Result<String, String> {
    let path = auth_source_path(app)?;
    let raw = fs::read_to_string(&path)
        .map_err(|e| format!("Could not read the YouTube Music session: {e}"))?;
    let json: Value = serde_json::from_str(&raw)
        .map_err(|e| format!("The saved YouTube Music session is invalid JSON: {e}"))?;
    json.get("Cookie")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|cookie| !cookie.is_empty())
        .map(str::to_string)
        .ok_or_else(|| "The saved YouTube Music session does not contain cookies.".to_string())
}

fn player_window(app: &AppHandle) -> Result<WebviewWindow, String> {
    if let Some(window) = app.get_webview_window(PLAYER_LABEL) {
        return Ok(window);
    }

    let url = tauri::Url::parse("about:blank").map_err(|e| e.to_string())?;
    WebviewWindowBuilder::new(app, PLAYER_LABEL, WebviewUrl::External(url))
        .title("YTM Playback Engine")
        .inner_size(2.0, 2.0)
        .resizable(false)
        .visible(false)
        .focusable(false)
        .skip_taskbar(true)
        .additional_browser_args(
            "--autoplay-policy=no-user-gesture-required --disable-features=msWebOOUI,msPdfOOUI,msSmartScreenProtection",
        )
        .build()
        .map_err(|e| format!("Could not start the official YouTube Music playback engine: {e}"))
}

fn install_session(window: &WebviewWindow, cookie_header: &str) -> Result<usize, String> {
    let mut installed = 0usize;

    for pair in cookie_header.split(';') {
        let Some((name, value)) = pair.trim().split_once('=') else {
            continue;
        };
        let name = name.trim();
        let value = value.trim();
        if name.is_empty() || value.is_empty() {
            continue;
        }

        // __Host- cookies cannot carry a Domain attribute. They are not required
        // for the captured YTM auth session, so skip them instead of installing an
        // invalid cookie into WebView2.
        if name.starts_with("__Host-") {
            continue;
        }

        let cookie = Cookie::build((name.to_owned(), value.to_owned()))
            .domain(".youtube.com")
            .path("/")
            .secure(true)
            .build();
        window
            .set_cookie(cookie)
            .map_err(|e| format!("Could not install YouTube cookie '{name}' into WebView2: {e}"))?;
        installed += 1;
    }

    if installed == 0 {
        Err("The saved YouTube Music session contained no usable YouTube cookies.".into())
    } else {
        Ok(installed)
    }
}

fn validate_video_id(video_id: &str) -> Result<(), String> {
    let video_id = video_id.trim();
    if video_id.is_empty()
        || video_id.len() > 64
        || !video_id
            .chars()
            .all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
    {
        return Err("Track has an invalid YouTube video id.".into());
    }
    Ok(())
}

fn arm_media(window: WebviewWindow, autoplay: bool, volume: f64) {
    tauri::async_runtime::spawn(async move {
        tokio::time::sleep(Duration::from_millis(700)).await;
        let autoplay = if autoplay { "true" } else { "false" };
        let volume = volume.clamp(0.0, 1.0);
        let script = format!(
            r#"(() => {{
  const desiredPlay = {autoplay};
  const desiredVolume = {volume};
  let attempts = 0;
  const apply = () => {{
    const media = document.querySelector('video, audio');
    if (!media) {{
      if (attempts++ < 80) setTimeout(apply, 250);
      return;
    }}
    media.volume = desiredVolume;
    if (desiredPlay) {{
      media.play().catch(() => {{}});
    }} else {{
      media.pause();
    }}
  }};
  apply();
}})();"#
        );
        let _ = window.eval(script);
    });
}

pub async fn load_track(
    app: &AppHandle,
    video_id: &str,
    autoplay: bool,
    volume: f64,
) -> Result<(), String> {
    validate_video_id(video_id)?;
    let cookie_header = read_cookie_header(app)?;
    let window = player_window(app)?;
    install_session(&window, &cookie_header)?;

    // Give WebView2 a moment to commit cookie changes before the authenticated
    // navigation. Playback after this point is entirely YouTube's official player.
    tokio::time::sleep(Duration::from_millis(80)).await;
    let url = tauri::Url::parse(&format!(
        "https://music.youtube.com/watch?v={}",
        video_id.trim()
    ))
    .map_err(|e| e.to_string())?;
    window
        .navigate(url)
        .map_err(|e| format!("Could not open the track in YouTube Music: {e}"))?;
    arm_media(window, autoplay, volume);
    Ok(())
}

pub fn control(app: &AppHandle, action: &str, value: Option<f64>) -> Result<(), String> {
    let window = app
        .get_webview_window(PLAYER_LABEL)
        .ok_or_else(|| "Playback has not been started yet.".to_string())?;

    let script = match action {
        "play_pause" => r#"(() => {
  const media = document.querySelector('video, audio');
  if (!media) return;
  if (media.paused) media.play().catch(() => {}); else media.pause();
})();"#
            .to_string(),
        "seek" => {
            let seconds = value.ok_or_else(|| "Seek requires a position.".to_string())?;
            if !seconds.is_finite() {
                return Err("Seek position is invalid.".into());
            }
            format!(
                r#"(() => {{
  const media = document.querySelector('video, audio');
  if (!media) return;
  const target = {seconds};
  const duration = Number.isFinite(media.duration) ? media.duration : target;
  media.currentTime = Math.max(0, Math.min(target, duration));
}})();"#
            )
        }
        "volume" => {
            let volume = value.ok_or_else(|| "Volume requires a value.".to_string())?;
            if !volume.is_finite() {
                return Err("Volume value is invalid.".into());
            }
            let volume = volume.clamp(0.0, 1.0);
            format!(
                r#"(() => {{
  const media = document.querySelector('video, audio');
  if (media) media.volume = {volume};
}})();"#
            )
        }
        "play" => r#"(() => { const media = document.querySelector('video, audio'); if (media) media.play().catch(() => {}); })();"#.to_string(),
        "pause" => r#"(() => { const media = document.querySelector('video, audio'); if (media) media.pause(); })();"#.to_string(),
        _ => return Ok(()),
    };

    window
        .eval(script)
        .map_err(|e| format!("Could not control the YouTube Music player: {e}"))
}

fn decode_status(raw: &str) -> Result<WebMediaStatus, String> {
    if let Ok(status) = serde_json::from_str::<WebMediaStatus>(raw) {
        return Ok(status);
    }
    if let Ok(encoded) = serde_json::from_str::<String>(raw) {
        if let Ok(status) = serde_json::from_str::<WebMediaStatus>(&encoded) {
            return Ok(status);
        }
    }
    Err(format!("Could not decode playback state returned by WebView2: {raw}"))
}

pub async fn status(app: &AppHandle) -> Result<Option<WebMediaStatus>, String> {
    let Some(window) = app.get_webview_window(PLAYER_LABEL) else {
        return Ok(None);
    };

    let script = r#"(() => {
  try {
    const media = document.querySelector('video, audio');
    return {
      ready: !!media,
      position: media && Number.isFinite(media.currentTime) ? media.currentTime : 0,
      duration: media && Number.isFinite(media.duration) ? media.duration : 0,
      isPlaying: !!media && !media.paused && !media.ended,
      volume: media && Number.isFinite(media.volume) ? media.volume : 0.8,
      ended: !!media && media.ended,
      pageUrl: location.href || '',
      title: document.title || '',
      error: null
    };
  } catch (error) {
    return {
      ready: false,
      position: 0,
      duration: 0,
      isPlaying: false,
      volume: 0.8,
      ended: false,
      pageUrl: location.href || '',
      title: document.title || '',
      error: String(error)
    };
  }
})()"#;

    let (tx, rx) = oneshot::channel::<String>();
    window
        .eval_with_callback(script, move |raw| {
            let _ = tx.send(raw);
        })
        .map_err(|e| format!("Could not query the YouTube Music player: {e}"))?;

    let raw = tokio::time::timeout(Duration::from_millis(900), rx)
        .await
        .map_err(|_| "Timed out while reading playback state from WebView2.".to_string())?
        .map_err(|_| "Playback state callback was cancelled.".to_string())?;
    decode_status(&raw).map(Some)
}

pub fn reset(app: &AppHandle) {
    if let Some(window) = app.get_webview_window(PLAYER_LABEL) {
        let _ = window.clear_all_browsing_data();
        let _ = window.close();
    }
}

pub fn provider_name() -> &'static str {
    "youtube-music-official-webview"
}

pub fn player_home() -> &'static str {
    PLAYER_HOME
}
