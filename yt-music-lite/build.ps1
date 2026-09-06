param(
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Version = "1.0.4191.47"
$Packages = Join-Path $Root "packages"
$PackageDir = Join-Path $Packages "Microsoft.Web.WebView2.$Version"
$Nupkg = Join-Path $Packages "Microsoft.Web.WebView2.$Version.nupkg"
$BuildDir = Join-Path $Root "build"

New-Item -ItemType Directory -Force $Packages | Out-Null
New-Item -ItemType Directory -Force $BuildDir | Out-Null

if (-not (Test-Path $PackageDir)) {
    Write-Host "Downloading Microsoft.Web.WebView2 $Version ..." -ForegroundColor Cyan
    $Url = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$Version"
    Invoke-WebRequest -Uri $Url -OutFile $Nupkg -UseBasicParsing
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Force $PackageDir | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($Nupkg, $PackageDir)
}

$Core = Join-Path $PackageDir "lib\net462\Microsoft.Web.WebView2.Core.dll"
$WinForms = Join-Path $PackageDir "lib\net462\Microsoft.Web.WebView2.WinForms.dll"
$Loader = Join-Path $PackageDir "runtimes\win-x64\native\WebView2Loader.dll"

foreach ($File in @($Core, $WinForms, $Loader)) {
    if (-not (Test-Path $File)) {
        throw "Required WebView2 file not found: $File"
    }
}

$CscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$Csc = $CscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Csc) {
    throw ".NET Framework C# compiler was not found. Install .NET Framework 4.8 developer tools."
}

$Out = Join-Path $BuildDir "YTMusicLite.exe"
$Sources = @(
    (Join-Path $Root "Program.cs"),
    (Join-Path $Root "PlayerState.cs"),
    (Join-Path $Root "UiControls.cs"),
    (Join-Path $Root "Branding.cs"),
    (Join-Path $Root "SettingsForm.cs"),
    (Join-Path $Root "MiniPlayerForm.cs"),
    (Join-Path $Root "MenuStyler.cs"),
    (Join-Path $Root "UiPolish.cs"),
    (Join-Path $Root "OfficialPlayerMode.cs"),
    (Join-Path $Root "WebViewChromeFix.cs"),
    (Join-Path $Root "UpdateService.cs"),
    (Join-Path $Root "MainForm.cs")
)

$Args = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/langversion:5",
    "/out:$Out",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:Accessibility.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:$Core",
    "/reference:$WinForms"
) + $Sources

Write-Host "Building YT Music Lite 4.1.8 with C# 5 compatibility ..." -ForegroundColor Cyan
& $Csc $Args
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE"
}

$UpdaterOut = Join-Path $BuildDir "YTMusicLite.Updater.exe"
$UpdaterSource = Join-Path $Root "Updater.cs"
$UpdaterArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/langversion:5",
    "/out:$UpdaterOut",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    $UpdaterSource
)

Write-Host "Building updater helper ..." -ForegroundColor Cyan
& $Csc $UpdaterArgs
if ($LASTEXITCODE -ne 0) {
    throw "Updater compilation failed with exit code $LASTEXITCODE"
}

Copy-Item $Core $BuildDir -Force
Copy-Item $WinForms $BuildDir -Force
Copy-Item $Loader $BuildDir -Force
Copy-Item (Join-Path $Root "VERSION.txt") $BuildDir -Force

Write-Host ""
Write-Host "Build complete: $Out" -ForegroundColor Green

if ($Run) {
    Start-Process $Out
}
