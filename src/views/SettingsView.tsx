import { useEffect, useState } from "react";
import { listen } from "@tauri-apps/api/event";
import { api } from "../lib/tauri";
import type { AuthStatus } from "../types/music";
import type { UpdateProgress, UpdateStatus } from "../types/update";

export function SettingsView() {
  const [path, setPath] = useState("");
  const [status, setStatus] = useState<AuthStatus>({ configured: false, valid: false });
  const [busy, setBusy] = useState(false);
  const [loginBusy, setLoginBusy] = useState(false);
  const [loginMessage, setLoginMessage] = useState("");
  const [update, setUpdate] = useState<UpdateStatus | null>(null);
  const [updateBusy, setUpdateBusy] = useState(false);
  const [progress, setProgress] = useState<UpdateProgress | null>(null);

  useEffect(() => {
    api.authStatus()
      .then((s) => {
        setStatus(s);
        if (s.sourcePath) setPath(s.sourcePath);
      })
      .catch((e) => setStatus({ configured: false, valid: false, message: String(e) }));

    api.checkForUpdates()
      .then(setUpdate)
      .catch((e) => setUpdate({
        configured: true,
        available: false,
        currentVersion: "0.3.8",
        message: String(e)
      }));

    const unlisten = listen<UpdateProgress>("update-progress", (event) => setProgress(event.payload));
    return () => { void unlisten.then((fn) => fn()); };
  }, []);

  useEffect(() => {
    if (!loginBusy) return;
    let cancelled = false;

    const timer = window.setInterval(async () => {
      try {
        const result = await api.pollWebLogin();
        if (cancelled || !result) return;
        setStatus(result);
        if (result.sourcePath) setPath(result.sourcePath);
        setLoginMessage("Connected to the Google account you selected. Your normal Brave session was not touched.");
        setLoginBusy(false);
      } catch (e) {
        if (cancelled) return;
        setLoginMessage(String(e));
        setLoginBusy(false);
      }
    }, 1200);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [loginBusy]);

  async function signIn() {
    setLoginMessage("");
    setLoginBusy(true);
    try {
      await api.startWebLogin();
      setLoginMessage("A separate YTM Desktop Brave sign-in window opened. Keep your normal Brave open; choose or sign into the Google account you want and YTM Desktop will finish automatically.");
    } catch (e) {
      setLoginBusy(false);
      setLoginMessage(String(e));
    }
  }

  async function disconnect() {
    setBusy(true);
    try {
      setStatus(await api.clearAuth());
      setPath("");
      setLoginMessage("Disconnected.");
    } finally {
      setBusy(false);
    }
  }

  async function connect() {
    setBusy(true);
    try {
      setStatus(await api.configureAuth(path));
    } catch (e) {
      setStatus({ configured: true, valid: false, sourcePath: path, message: String(e) });
    } finally {
      setBusy(false);
    }
  }

  async function checkUpdate() {
    setUpdateBusy(true);
    setProgress(null);
    try {
      setUpdate(await api.checkForUpdates());
    } catch (e) {
      setUpdate((prev) => ({
        configured: true,
        available: false,
        currentVersion: prev?.currentVersion || "0.3.8",
        source: prev?.source,
        message: String(e)
      }));
    } finally {
      setUpdateBusy(false);
    }
  }

  async function installUpdate() {
    setUpdateBusy(true);
    setProgress({ downloaded: 0, total: null, finished: false, stage: "download" });
    try {
      await api.installUpdate();
    } catch (e) {
      setUpdate((prev) => ({
        configured: true,
        available: prev?.available ?? false,
        currentVersion: prev?.currentVersion || "0.3.8",
        version: prev?.version,
        notes: prev?.notes,
        publishedAt: prev?.publishedAt,
        source: prev?.source,
        message: String(e)
      }));
      setUpdateBusy(false);
    }
  }

  const percent = progress?.total
    ? Math.min(100, Math.round(progress.downloaded / progress.total * 100))
    : null;

  const stageLabel = progress?.stage === "verify"
    ? "Verifying update"
    : progress?.stage === "install"
      ? "Starting installer"
      : "Downloading update";

  return <div className="page settings">
    <header className="simple-head"><p>SETTINGS</p><h1>Connection & updates</h1></header>

    <div className="settings-card">
      <div className={`status-dot ${status.valid ? "ok" : ""}`}/>
      <div className="auth-summary">
        <h3>{status.valid ? "YouTube Music connected" : "YouTube Music not connected"}</h3>
        <p>{status.message || "Sign in once and YTM Desktop will keep the session for you."}</p>
      </div>
      {status.valid && <button className="secondary-button auth-disconnect" onClick={disconnect} disabled={busy}>Disconnect</button>}
    </div>

    <div className="settings-card column auth-card">
      <label>Account</label>
      <h3>Sign in with Google</h3>
      <p className="muted">YTM Desktop now uses its own persistent Brave authentication profile. Your everyday Brave can remain open and is never modified or locked. The first time, choose or sign into the Google account you want. That auth profile keeps the Google login for future sign-ins, then briefly hands the selected YouTube Music session back to YTM Desktop automatically.</p>
      <div className="auth-actions">
        <button className="install-button auth-signin" onClick={signIn} disabled={loginBusy}>
          {loginBusy ? "Waiting for Google sign-in…" : status.valid ? "Switch account" : "Sign in with Google"}
        </button>
        {loginMessage && <span className="channel-message">{loginMessage}</span>}
      </div>

      <details className="manual-auth">
        <summary>Advanced: use an existing browser.json</summary>
        <div className="path-row">
          <input value={path} onChange={(e) => setPath(e.target.value)} placeholder="C:\\Users\\You\\Downloads\\browser.json"/>
          <button onClick={connect} disabled={!path || busy}>{busy ? "Checking…" : "Connect file"}</button>
        </div>
        <p>The manual method is kept only as a fallback. Session credentials stay on this PC.</p>
      </details>
    </div>

    <div className="settings-card column update-card">
      <div className="update-head">
        <div>
          <label>App updates</label>
          <h3>YTM Desktop {update?.currentVersion || "0.3.8"}</h3>
          <p className="muted updater-subtitle">Stable channel · GitHub Releases</p>
        </div>
        <button className="secondary-button" onClick={checkUpdate} disabled={updateBusy}>
          {updateBusy && !progress ? "Checking…" : "Check for updates"}
        </button>
      </div>

      <p className={update?.available ? "update-available" : "muted"}>
        {update?.message || "Checking the YTM Desktop release channel…"}
      </p>

      {update?.available && <div className="update-release">
        <div className="release-version-row">
          <div>
            <span className="release-kicker">UPDATE AVAILABLE</span>
            <b>Version {update.version}</b>
          </div>
          {update.publishedAt && <span className="release-date">{new Date(update.publishedAt).toLocaleDateString()}</span>}
        </div>
        {update.notes && <p>{update.notes}</p>}
        <button className="install-button" onClick={installUpdate} disabled={updateBusy}>
          {progress ? "Installing…" : "Download & install"}
        </button>
      </div>}

      {progress && !progress.finished && <div className="update-progress">
        <div><span>{stageLabel}</span><span>{percent !== null && progress.stage === "download" ? `${percent}%` : ""}</span></div>
        <div className="progress-track"><div style={{width: `${progress.stage === "download" ? (percent ?? 12) : 100}%`}}/></div>
      </div>}

      {progress?.finished && <p className="update-available">Update verified. YTM Desktop will close, install the update, and relaunch the installed app.</p>}
      {update?.source && <p className="update-source">Release source: {update.source}</p>}
    </div>
  </div>;
}
