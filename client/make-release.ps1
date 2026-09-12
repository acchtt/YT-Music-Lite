$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$release = Join-Path $root 'release'
$payload = Join-Path $release 'payload'
$version = '7.2.2'
$zipName = "YTMusicLite-v$version-win-x64.zip"
$zip = Join-Path $release $zipName
$required = @('YTMusicLite.exe', 'YTMusicLite.Updater.exe', 'mpv.exe', 'yt-dlp.exe', 'deno.exe', 'VERSION.txt')
foreach ($name in $required) { if (-not (Test-Path (Join-Path $build $name))) { throw "Missing release file: $name" } }
Remove-Item $payload -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $payload | Out-Null
foreach ($name in $required) { Copy-Item (Join-Path $build $name) $payload -Force }
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
Set-Content -Path "$zip.sha256" -Value "$hash  $zipName" -Encoding ASCII
Write-Host "Release package: $zip" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Green
