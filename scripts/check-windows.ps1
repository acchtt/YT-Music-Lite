$ErrorActionPreference = "SilentlyContinue"
Write-Host "YTM Desktop prerequisite check" -ForegroundColor Cyan
Write-Host ""

function Check-Command($name, $args) {
  $cmd = Get-Command $name
  if ($null -eq $cmd) {
    Write-Host "[MISSING] $name" -ForegroundColor Red
    return $false
  }
  $version = & $name $args 2>$null | Select-Object -First 1
  Write-Host "[OK] $name $version" -ForegroundColor Green
  return $true
}

$node = Check-Command "node" "--version"
$npm = Check-Command "npm" "--version"
$cargo = Check-Command "cargo" "--version"
$rustc = Check-Command "rustc" "--version"

Write-Host ""
if (-not $node) { Write-Host "Install Node.js 20.19+ from nodejs.org." }
if (-not $cargo -or -not $rustc) { Write-Host "Install Rust using rustup (stable MSVC toolchain)." }
Write-Host "Also ensure Visual Studio Build Tools 2022 -> Desktop development with C++ and WebView2 Runtime are installed."
