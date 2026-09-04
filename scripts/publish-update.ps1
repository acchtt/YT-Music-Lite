param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
  [string]$Version
)

$ErrorActionPreference = "Stop"

$packagePath = Join-Path $PSScriptRoot "..\package.json"
$tauriPath = Join-Path $PSScriptRoot "..\src-tauri\tauri.conf.json"
$cargoPath = Join-Path $PSScriptRoot "..\src-tauri\Cargo.toml"

$package = Get-Content $packagePath -Raw | ConvertFrom-Json
$package.version = $Version
$package | ConvertTo-Json -Depth 20 | Set-Content -Encoding utf8 $packagePath

$tauri = Get-Content $tauriPath -Raw | ConvertFrom-Json
$tauri.version = $Version
$tauri | ConvertTo-Json -Depth 30 | Set-Content -Encoding utf8 $tauriPath

$cargo = Get-Content $cargoPath -Raw
$cargo = [regex]::Replace($cargo, '(?m)^(version\s*=\s*")([^"]+)(")', "`${1}$Version`${3}", 1)
Set-Content -Encoding utf8 $cargoPath $cargo

# Keep the fallback display version in Settings aligned with the native package version.
$settingsPath = Join-Path $PSScriptRoot "..\src\views\SettingsView.tsx"
$settings = Get-Content $settingsPath -Raw
$settings = [regex]::Replace($settings, 'currentVersion: "[0-9A-Za-z.-]+"', "currentVersion: `"$Version`"")
$settings = [regex]::Replace($settings, '\|\| "[0-9A-Za-z.-]+"', "|| `"$Version`"")
Set-Content -Encoding utf8 $settingsPath $settings

$titlebarPath = Join-Path $PSScriptRoot "..\src\components\Titlebar.tsx"
$titlebar = Get-Content $titlebarPath -Raw
$titlebar = [regex]::Replace($titlebar, '<span className="version">[^<]+</span>', "<span className=`"version`">$Version</span>")
Set-Content -Encoding utf8 $titlebarPath $titlebar

git add package.json src-tauri/Cargo.toml src-tauri/tauri.conf.json src/views/SettingsView.tsx src/components/Titlebar.tsx
git commit -m "Release $Version"
git tag "app-v$Version"
git push origin main
git push origin "app-v$Version"

Write-Host "Release app-v$Version pushed. GitHub Actions is building the installer." -ForegroundColor Green
