# YTM Desktop — Option 3

Windows standalone YouTube Music client using Tauri + React + TypeScript with a Rust service layer. The visible application is custom UI; YouTube Music's website is not used as the normal player UI.

## Current build: 0.2.6

### Account

Settings now provides **Sign in with Google**. A temporary YouTube Music WebView is used only to establish the account session, then the session is stored locally for the Rust API client. `browser.json` remains available only as an Advanced fallback.

### Built-in updates

0.2.6 removes the old repository/public-key fields from Settings. The update channel is built into the app:

`acchtt/YTM-Desktop` → GitHub Releases → Windows NSIS installer.

Settings > App updates can:

- check for a newer stable release;
- show its release notes;
- download the Windows installer;
- show download progress;
- verify the release SHA-256;
- close YTM Desktop and install silently;
- relaunch an installed build after a successful update.

There is no updater key or URL for the user to paste into the app.

The release repository itself must exist once. From the project root run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-update-repo.ps1
```

Then publish the baseline release:

```powershell
git tag app-v0.2.6
git push origin app-v0.2.6
```

Future releases are one command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-update.ps1 -Version 0.2.7
```

See `docs/UPDATES.md`.

## Development

```powershell
npm install
npm run tauri:dev
```

## Windows release build

```powershell
npm run tauri:build -- --bundles nsis
```

## Project layout

```text
src/                 React UI
src-tauri/           Rust/Tauri backend
  src/music_service.rs
  src/playback_resolver.rs
  src/update_service.rs
scripts/             Windows setup/release helpers
.github/workflows/   Windows GitHub Release pipeline
```

## Security notes

Account session files and `browser.json` are ignored by Git. The built-in updater accepts installers only from the hard-coded YTM Desktop GitHub release channel and verifies the release SHA-256 before launching the installer. Windows code signing can be added later as a second publisher-identity layer.
