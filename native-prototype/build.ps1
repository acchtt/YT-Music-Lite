$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $nativeRoot
$nativeBuild = Join-Path $nativeRoot 'build'
New-Item -ItemType Directory -Force $nativeBuild | Out-Null
$csc = @(
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'C# compiler not found' }

$brandRoot = Join-Path $repositoryRoot 'yt-music-lite'
$assetTool = Join-Path $nativeBuild 'BuildBrandAssets.exe'
& $csc /nologo /target:exe /langversion:5 "/out:$assetTool" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $brandRoot 'Branding.cs') (Join-Path $brandRoot 'BuildBrandAssets.cs')
if ($LASTEXITCODE -ne 0) { throw 'Brand asset compilation failed' }
& $assetTool $nativeBuild
if ($LASTEXITCODE -ne 0) { throw 'Brand asset generation failed' }

$output = Join-Path $nativeBuild 'YTMusicLite.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 "/win32icon:$(Join-Path $nativeBuild 'YTMusicLite.ico')" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$output" (Join-Path $nativeRoot 'NativePlayer.cs') (Join-Path $nativeRoot 'SpotifyControls.cs') (Join-Path $nativeRoot 'MusicUi.cs') (Join-Path $nativeRoot 'UpdateService.cs')
if ($LASTEXITCODE -ne 0) { throw 'Native app compilation failed' }

$updater = Join-Path $nativeBuild 'YTMusicLite.Updater.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 "/out:$updater" /reference:System.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $brandRoot 'Updater.cs')
if ($LASTEXITCODE -ne 0) { throw 'Updater compilation failed' }
Set-Content -Path (Join-Path $nativeBuild 'VERSION.txt') -Value '5.1.0' -Encoding ASCII
Write-Host "Native build complete: $output" -ForegroundColor Green
