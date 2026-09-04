param(
  [string]$Repository = "acchtt/YTM-Desktop"
)

Write-Host "The old signing-key updater setup was removed in 0.2.6." -ForegroundColor Yellow
Write-Host "Starting the built-in GitHub release channel setup instead..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "setup-update-repo.ps1") -Repository $Repository
