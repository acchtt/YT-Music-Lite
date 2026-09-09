$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
New-Item -ItemType Directory -Force $build | Out-Null
$csc = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw '.NET Framework C# compiler not found.' }

$commonReferences = @(
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll'
)

$assetTool = Join-Path $build 'BuildAssets.exe'
& $csc /nologo /target:exe /langversion:5 "/out:$assetTool" $commonReferences (Join-Path $root 'Theme.cs') (Join-Path $root 'Branding.cs') (Join-Path $root 'BuildAssets.cs')
if ($LASTEXITCODE -ne 0) { throw 'Brand asset compilation failed.' }
& $assetTool $build
if ($LASTEXITCODE -ne 0) { throw 'Brand asset generation failed.' }

$sources = Get-ChildItem $root -Filter '*.cs' | Where-Object { $_.Name -notin @('BuildAssets.cs', 'Updater.cs') } | ForEach-Object { $_.FullName }
$app = Join-Path $build 'YTMusicLite.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 "/win32icon:$(Join-Path $build 'YTMusicLite.ico')" "/out:$app" $commonReferences $sources
if ($LASTEXITCODE -ne 0) { throw 'Client compilation failed.' }

$updater = Join-Path $build 'YTMusicLite.Updater.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 "/out:$updater" /reference:System.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $root 'Updater.cs')
if ($LASTEXITCODE -ne 0) { throw 'Updater compilation failed.' }

Set-Content -Path (Join-Path $build 'VERSION.txt') -Value '6.0.3' -Encoding ASCII
Write-Host "Client build complete: $app" -ForegroundColor Green
