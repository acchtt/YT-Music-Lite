# YTM Desktop built-in updates

YTM Desktop 0.2.6 uses a built-in Windows updater backed by GitHub Releases at `acchtt/YTM-Desktop`.

The app itself requires no update configuration, updater key, Python tool, or manual URL. Settings > App updates always checks the stable release channel.

## One-time repository setup

From the project root on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-update-repo.ps1
```

The script creates/links `acchtt/YTM-Desktop`, pushes the source, and leaves the GitHub Actions release workflow ready.

Publish the baseline 0.2.6 release once:

```powershell
git tag app-v0.2.6
git push origin app-v0.2.6
```

GitHub Actions builds the NSIS installer and publishes two assets:

- `*-setup.exe`
- `*-setup.exe.sha256`

## Publishing later updates

After making changes, publish a new version with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-update.ps1 -Version 0.2.7
```

The app checks GitHub's latest stable release. When a newer semantic version is found, it downloads the NSIS installer, verifies the SHA-256 published with that release, waits for the running app to close, installs silently, and relaunches the installed build.

Development builds can check and download the same release feed, but they intentionally do not relaunch the old `target/debug` executable after installing a release build.

## Trust model

The updater downloads only from the hard-coded `acchtt/YTM-Desktop` GitHub release channel and refuses to run an installer unless its SHA-256 matches the checksum asset published in the same release. This removes the previous per-user public-key/configuration workflow. Windows code signing can be added later for an additional publisher-identity layer.
