use std::{
    fs::{self, File},
    io::Write,
    path::{Path, PathBuf},
    process::Command,
};

use futures_util::StreamExt;
use semver::Version;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use tauri::{AppHandle, Emitter};

const UPDATE_REPOSITORY: &str = "acchtt/YTM-Desktop";
const GITHUB_API_BASE: &str = "https://api.github.com";

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateStatus {
    pub configured: bool,
    pub available: bool,
    pub current_version: String,
    pub version: Option<String>,
    pub notes: Option<String>,
    pub published_at: Option<String>,
    pub source: Option<String>,
    pub message: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct UpdateProgress {
    downloaded: u64,
    total: Option<u64>,
    finished: bool,
    stage: String,
}

#[derive(Debug, Clone, Deserialize)]
struct GithubRelease {
    tag_name: String,
    body: Option<String>,
    published_at: Option<String>,
    html_url: String,
    assets: Vec<GithubAsset>,
}

#[derive(Debug, Clone, Deserialize)]
struct GithubAsset {
    name: String,
    browser_download_url: String,
    size: u64,
}

fn release_api_url() -> String {
    format!("{GITHUB_API_BASE}/repos/{UPDATE_REPOSITORY}/releases/latest")
}

fn parse_release_version(tag: &str) -> Result<Version, String> {
    let value = tag
        .trim()
        .strip_prefix("app-v")
        .or_else(|| tag.trim().strip_prefix('v'))
        .unwrap_or(tag.trim());
    Version::parse(value).map_err(|e| format!("Release tag '{tag}' is not a valid app version: {e}"))
}

fn client(app: &AppHandle) -> Result<reqwest::Client, String> {
    reqwest::Client::builder()
        .user_agent(format!("YTM-Desktop/{}", app.package_info().version))
        .redirect(reqwest::redirect::Policy::limited(10))
        .build()
        .map_err(|e| e.to_string())
}

async fn latest_release(app: &AppHandle) -> Result<GithubRelease, String> {
    let url = release_api_url();
    let response = client(app)?
        .get(&url)
        .header("Accept", "application/vnd.github+json")
        .header("X-GitHub-Api-Version", "2022-11-28")
        .send()
        .await
        .map_err(|e| format!("Could not reach the update server: {e}"))?;

    if response.status() == reqwest::StatusCode::NOT_FOUND {
        return Err(format!(
            "The YTM Desktop release channel is not online yet ({UPDATE_REPOSITORY}). Publish the first GitHub Release, then in-app updates work automatically."
        ));
    }

    if response.status() == reqwest::StatusCode::FORBIDDEN {
        return Err("GitHub temporarily refused the update check (rate limit). Try again in a few minutes.".into());
    }

    response
        .error_for_status()
        .map_err(|e| format!("Update server returned an error: {e}"))?
        .json::<GithubRelease>()
        .await
        .map_err(|e| format!("Invalid update response: {e}"))
}

fn installer_assets<'a>(release: &'a GithubRelease) -> Result<(&'a GithubAsset, &'a GithubAsset), String> {
    let installer = release
        .assets
        .iter()
        .find(|asset| {
            let name = asset.name.to_ascii_lowercase();
            name.ends_with("-setup.exe") && !name.ends_with(".sha256")
        })
        .or_else(|| {
            release.assets.iter().find(|asset| {
                let name = asset.name.to_ascii_lowercase();
                name.ends_with(".exe") && !name.ends_with(".sha256")
            })
        })
        .ok_or_else(|| "The latest release does not contain a Windows NSIS installer.".to_string())?;

    let checksum_name = format!("{}.sha256", installer.name);
    let checksum = release
        .assets
        .iter()
        .find(|asset| asset.name.eq_ignore_ascii_case(&checksum_name))
        .ok_or_else(|| format!("The release is missing {checksum_name}. Refusing to install an unverified update."))?;

    Ok((installer, checksum))
}

pub async fn check(app: &AppHandle) -> Result<UpdateStatus, String> {
    let current_version = app.package_info().version.to_string();
    let current = Version::parse(&current_version)
        .map_err(|e| format!("Current app version is invalid: {e}"))?;

    let release = match latest_release(app).await {
        Ok(release) => release,
        Err(message) => {
            return Ok(UpdateStatus {
                configured: true,
                available: false,
                current_version,
                version: None,
                notes: None,
                published_at: None,
                source: Some(format!("https://github.com/{UPDATE_REPOSITORY}/releases")),
                message,
            });
        }
    };

    let latest = parse_release_version(&release.tag_name)?;
    let available = latest > current;

    // Validate that a usable Windows bundle exists before advertising it.
    if available {
        installer_assets(&release)?;
    }

    Ok(UpdateStatus {
        configured: true,
        available,
        current_version,
        version: Some(latest.to_string()),
        notes: release.body,
        published_at: release.published_at,
        source: Some(release.html_url),
        message: if available {
            format!("YTM Desktop {latest} is ready to install.")
        } else {
            "You are up to date.".into()
        },
    })
}

fn safe_asset_name(name: &str) -> Result<&str, String> {
    Path::new(name)
        .file_name()
        .and_then(|v| v.to_str())
        .filter(|v| !v.is_empty())
        .ok_or_else(|| "Invalid update asset name.".to_string())
}

async fn download_installer(
    app: &AppHandle,
    client: &reqwest::Client,
    asset: &GithubAsset,
) -> Result<(PathBuf, String), String> {
    let filename = safe_asset_name(&asset.name)?;
    let update_dir = std::env::temp_dir().join("YTM-Desktop-Update");
    fs::create_dir_all(&update_dir).map_err(|e| e.to_string())?;
    let path = update_dir.join(filename);
    let _ = fs::remove_file(&path);

    let response = client
        .get(&asset.browser_download_url)
        .send()
        .await
        .map_err(|e| format!("Could not download the update: {e}"))?
        .error_for_status()
        .map_err(|e| format!("Update download failed: {e}"))?;

    let total = response.content_length().or(Some(asset.size)).filter(|v| *v > 0);
    let mut stream = response.bytes_stream();
    let mut file = File::create(&path).map_err(|e| format!("Could not create update file: {e}"))?;
    let mut hasher = Sha256::new();
    let mut downloaded = 0u64;

    while let Some(chunk) = stream.next().await {
        let chunk = chunk.map_err(|e| format!("Update download interrupted: {e}"))?;
        file.write_all(&chunk)
            .map_err(|e| format!("Could not write update file: {e}"))?;
        hasher.update(&chunk);
        downloaded += chunk.len() as u64;
        let _ = app.emit(
            "update-progress",
            UpdateProgress {
                downloaded,
                total,
                finished: false,
                stage: "download".into(),
            },
        );
    }

    file.flush().map_err(|e| e.to_string())?;
    Ok((path, format!("{:x}", hasher.finalize())))
}

async fn expected_checksum(client: &reqwest::Client, asset: &GithubAsset) -> Result<String, String> {
    let text = client
        .get(&asset.browser_download_url)
        .send()
        .await
        .map_err(|e| format!("Could not download update checksum: {e}"))?
        .error_for_status()
        .map_err(|e| format!("Checksum download failed: {e}"))?
        .text()
        .await
        .map_err(|e| e.to_string())?;

    let hash = text
        .split_whitespace()
        .next()
        .unwrap_or("")
        .trim()
        .to_ascii_lowercase();
    if hash.len() != 64 || !hash.chars().all(|c| c.is_ascii_hexdigit()) {
        return Err("The release checksum file is invalid.".into());
    }
    Ok(hash)
}

#[cfg(target_os = "windows")]
fn launch_windows_installer(app: &AppHandle, installer: &Path) -> Result<(), String> {
    let pid = std::process::id();
    let installer = installer
        .canonicalize()
        .unwrap_or_else(|_| installer.to_path_buf());
    let current_exe = std::env::current_exe().map_err(|e| e.to_string())?;
    let is_dev = cfg!(debug_assertions);

    fn ps_quote(value: &Path) -> String {
        value.to_string_lossy().replace('\'', "''")
    }

    let relaunch = if is_dev {
        String::new()
    } else {
        format!(
            "; if ($p.ExitCode -eq 0 -and (Test-Path '{}')) {{ Start-Process -FilePath '{}' }}",
            ps_quote(&current_exe),
            ps_quote(&current_exe)
        )
    };

    let script = format!(
        "$oldPid={pid}; while (Get-Process -Id $oldPid -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 250 }}; \
         $p=Start-Process -FilePath '{}' -ArgumentList '/S' -PassThru -Wait{}",
        ps_quote(&installer),
        relaunch
    );

    Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-Command",
            &script,
        ])
        .spawn()
        .map_err(|e| format!("Could not start the Windows updater: {e}"))?;

    app.exit(0);
    Ok(())
}

#[cfg(not(target_os = "windows"))]
fn launch_windows_installer(_app: &AppHandle, _installer: &Path) -> Result<(), String> {
    Err("This updater build currently supports Windows only.".into())
}

pub async fn install(app: &AppHandle) -> Result<(), String> {
    let current = Version::parse(&app.package_info().version.to_string())
        .map_err(|e| format!("Current app version is invalid: {e}"))?;
    let release = latest_release(app).await?;
    let latest = parse_release_version(&release.tag_name)?;
    if latest <= current {
        return Err("No newer update is available.".into());
    }

    let (installer_asset, checksum_asset) = installer_assets(&release)?;
    let client = client(app)?;

    let expected = expected_checksum(&client, checksum_asset).await?;
    let (installer_path, actual) = download_installer(app, &client, installer_asset).await?;

    let _ = app.emit(
        "update-progress",
        UpdateProgress {
            downloaded: installer_asset.size,
            total: Some(installer_asset.size),
            finished: false,
            stage: "verify".into(),
        },
    );

    if actual != expected {
        let _ = fs::remove_file(&installer_path);
        return Err(format!(
            "Update verification failed. Expected SHA-256 {expected}, downloaded {actual}. The installer was deleted."
        ));
    }

    let _ = app.emit(
        "update-progress",
        UpdateProgress {
            downloaded: installer_asset.size,
            total: Some(installer_asset.size),
            finished: true,
            stage: "install".into(),
        },
    );

    launch_windows_installer(app, &installer_path)
}
