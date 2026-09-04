param(
  [string]$Repository = "acchtt/YTM-Desktop"
)

$ErrorActionPreference = "Stop"

Write-Host "YTM Desktop update channel setup" -ForegroundColor Cyan
Write-Host "Repository: $Repository"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw "Git is required. Install it with: winget install --id Git.Git"
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  Write-Host "GitHub CLI is missing. Installing with winget..." -ForegroundColor Yellow
  winget install --id GitHub.cli --accept-package-agreements --accept-source-agreements
  throw "GitHub CLI was installed. Open a new PowerShell window and run this script again."
}

# Authenticate without letting a non-zero native exit code terminate the script.
$oldNativePref = $null
$hasNativePref = Test-Path variable:PSNativeCommandUseErrorActionPreference
if ($hasNativePref) {
  $oldNativePref = $PSNativeCommandUseErrorActionPreference
  $PSNativeCommandUseErrorActionPreference = $false
}

try {
  gh auth status 2>$null | Out-Null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "Opening GitHub sign-in..." -ForegroundColor Yellow
    gh auth login --hostname github.com --git-protocol https --web
    if ($LASTEXITCODE -ne 0) { throw "GitHub sign-in did not complete." }
  }

  if (-not (Test-Path ".git")) {
    git init
    if ($LASTEXITCODE -ne 0) { throw "git init failed." }
  }

  # Missing repo is expected on first setup. Probe it without failing the script.
  $repoOutput = gh repo view $Repository --json name 2>$null
  $exists = ($LASTEXITCODE -eq 0)

  if (-not $exists) {
    Write-Host "Repository does not exist yet. Creating $Repository..." -ForegroundColor Yellow
    gh repo create $Repository --public --description "Custom standalone YouTube Music desktop client"
    if ($LASTEXITCODE -ne 0) { throw "Could not create GitHub repository $Repository." }
  } else {
    Write-Host "Repository already exists." -ForegroundColor DarkGray
  }

  # Ensure origin points at the updater repository.
  $origin = ""
  git remote get-url origin 2>$null | ForEach-Object { $origin = $_ }
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($origin)) {
    git remote add origin "https://github.com/$Repository.git"
    if ($LASTEXITCODE -ne 0) { throw "Could not add GitHub remote." }
  } elseif ($origin -ne "https://github.com/$Repository.git" -and $origin -ne "git@github.com:$Repository.git") {
    Write-Host "Updating existing origin remote: $origin" -ForegroundColor Yellow
    git remote set-url origin "https://github.com/$Repository.git"
    if ($LASTEXITCODE -ne 0) { throw "Could not update GitHub remote." }
  }

  # browser.json / login-state files must remain ignored.
  if (Test-Path "browser.json") {
    $tracked = git ls-files --error-unmatch browser.json 2>$null
    if ($LASTEXITCODE -eq 0) {
      throw "browser.json is tracked by Git. Remove it from Git before continuing: git rm --cached browser.json"
    }
  }

  git add .
  if ($LASTEXITCODE -ne 0) { throw "git add failed." }

  # Show exactly what is about to be committed.
  Write-Host ""
  Write-Host "Files staged for the updater repository:" -ForegroundColor Cyan
  git status --short

  git rev-parse --verify HEAD 2>$null | Out-Null
  $hasCommit = ($LASTEXITCODE -eq 0)

  if (-not $hasCommit) {
    git commit -m "YTM Desktop 0.2.6"
    if ($LASTEXITCODE -ne 0) {
      throw "Initial commit failed. If Git asks for your identity, configure git user.name and user.email first."
    }
  } else {
    $dirty = git status --porcelain
    if ($dirty) {
      git commit -m "Configure built-in updater"
      if ($LASTEXITCODE -ne 0) { throw "Git commit failed." }
    }
  }

  git branch -M main
  if ($LASTEXITCODE -ne 0) { throw "Could not switch branch to main." }

  git push -u origin main
  if ($LASTEXITCODE -ne 0) { throw "Could not push main branch." }

  Write-Host ""
  Write-Host "Update repository is ready." -ForegroundColor Green
  Write-Host "Repository: https://github.com/$Repository" -ForegroundColor DarkGray
  Write-Host ""
  Write-Host "Publish 0.2.6 as the baseline release with:" -ForegroundColor Cyan
  Write-Host "  git tag app-v0.2.6"
  Write-Host "  git push origin app-v0.2.6"
  Write-Host ""
  Write-Host "GitHub Actions will then build and publish the Windows installer."
}
finally {
  if ($hasNativePref) {
    $PSNativeCommandUseErrorActionPreference = $oldNativePref
  }
}
