$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "YT Music Lite 7 is Windows-only. Run this script on Windows 10 or 11."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw "Node.js 20.19 or newer is required." }
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) { throw "Rust stable with the MSVC target is required." }

npm ci
npm run check
npm run build
npm run tauri:build -- --bundles nsis

Write-Host "Windows installer created under src-tauri\target\release\bundle\nsis" -ForegroundColor Green
