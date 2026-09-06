# YT Music Lite

A lightweight Windows app for YouTube Music, built with C# WinForms and Microsoft WebView2. YouTube Music provides the library, search and official player; the desktop shell adds a mini player, tray controls, sleep mode and verified updates.

## Build and run

On Windows with .NET Framework 4.8 and PowerShell:

```powershell
.\yt-music-lite\build.ps1 -Run
```

The build script downloads its WebView2 SDK dependency. If the app reports a missing WebView2 runtime, run `yt-music-lite/install-webview2-runtime.cmd`.

## Desktop controls

- Use Back/Forward to navigate; unavailable navigation actions are disabled.
- Open the mini player from the toolbar or tray. Its last position and **Always on top** preference are remembered.
- Tab through native controls. Use Space/Enter for buttons, Space for switches, and arrow keys for sliders. Seeking moves five seconds at a time; Home/End jump to the beginning/end.
- **Pause and sleep** pauses music and reduces resource use. **Resume** returns to the player; playback stays paused until you press Play.
- Closing/minimizing can keep the app in the tray. Use the tray menu's **Exit** to quit, or change this behavior in Settings.
- **Settings → About and updates** shows release notes, download progress and verification status. Download while listening, then choose **Restart to update** when ready.

## Source and verification

The current Windows app lives in `yt-music-lite/`. `src/` and `src-tauri/` contain the earlier React/Tauri implementation and are not used by the current native release workflow.

`.github/workflows/desktop-ux-check.yml` builds the Windows app and runs UI regression checks, including scaled Settings captures. Those captures exercise layout scaling; real multi-monitor DPI changes and screen-reader behavior still need manual Windows checks.

Releases use the `ytmlite-v*` channel in this repository. The updater verifies SHA-256 before preparing an update and again before installation.
