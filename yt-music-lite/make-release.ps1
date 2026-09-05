param(
    [switch]$BuildFirst
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildDir = Join-Path $Root "build"
$Version = "4.1.3"
$ReleaseDir = Join-Path $Root "release"
$PayloadDir = Join-Path $ReleaseDir "payload"
$ZipName = "YTMusicLite-v$Version-win-x64.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName
$ShaPath = "$ZipPath.sha256"

if ($BuildFirst) {
    & (Join-Path $Root "build.ps1")
}

$Required = @(
    "YTMusicLite.exe",
    "YTMusicLite.Updater.exe",
    "Microsoft.Web.WebView2.Core.dll",
    "Microsoft.Web.WebView2.WinForms.dll",
    "WebView2Loader.dll",
    "VERSION.txt"
)

foreach ($Name in $Required) {
    $Path = Join-Path $BuildDir $Name
    if (-not (Test-Path $Path)) {
        throw "Missing release file: $Path. Run build.ps1 first."
    }
}

Remove-Item $PayloadDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $PayloadDir | Out-Null
New-Item -ItemType Directory -Force $ReleaseDir | Out-Null

foreach ($Name in $Required) {
    Copy-Item (Join-Path $BuildDir $Name) $PayloadDir -Force
}

Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $PayloadDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal
$Hash = (Get-FileHash -Algorithm SHA256 $ZipPath).Hash.ToLowerInvariant()
Set-Content -Path $ShaPath -Value "$Hash  $ZipName" -Encoding ASCII

Write-Host "Release package: $ZipPath" -ForegroundColor Green
Write-Host "SHA-256: $Hash" -ForegroundColor Green
Write-Host "GitHub tag expected by updater: ytmlite-v$Version" -ForegroundColor Cyan
