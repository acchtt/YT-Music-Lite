using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class UpdateService
    {
        public const string CurrentVersion = "4.1.1";
        public const string RepositoryOwner = "acchtt";
        public const string RepositoryName = "YT-Music-Lite";
        public const string ReleaseTagPrefix = "ytmlite-v";

        private readonly JavaScriptSerializer json;

        public UpdateService()
        {
            json = new JavaScriptSerializer();
        }

        public async Task<UpdateCheckResult> CheckAsync()
        {
            string url = "https://api.github.com/repos/" + RepositoryOwner + "/" + RepositoryName + "/releases?per_page=20";
            string body = await DownloadStringAsync(url);
            object[] releases = json.Deserialize<object[]>(body);
            if (releases == null)
            {
                return UpdateCheckResult.NoUpdate("No releases were returned by GitHub.");
            }

            foreach (object item in releases)
            {
                DictionaryAdapter release = new DictionaryAdapter(item);
                if (release.GetBool("draft") || release.GetBool("prerelease")) continue;

                string tag = release.GetString("tag_name");
                if (string.IsNullOrEmpty(tag) || !tag.StartsWith(ReleaseTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                string versionText = tag.Substring(ReleaseTagPrefix.Length);
                Version latest;
                Version current;
                if (!TryParseVersion(versionText, out latest) || !TryParseVersion(CurrentVersion, out current)) continue;
                if (latest.CompareTo(current) <= 0)
                {
                    return UpdateCheckResult.NoUpdate("YT Music Lite is up to date.");
                }

                object[] assets = release.GetArray("assets");
                if (assets == null) continue;

                string expectedName = "YTMusicLite-v" + latest.ToString() + "-win-x64.zip";
                foreach (object assetObject in assets)
                {
                    DictionaryAdapter asset = new DictionaryAdapter(assetObject);
                    string name = asset.GetString("name");
                    if (!string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase)) continue;

                    string downloadUrl = asset.GetString("browser_download_url");
                    string digest = asset.GetString("digest");
                    string sha256 = ParseSha256Digest(digest);
                    string checksumUrl = FindChecksumUrl(assets, expectedName + ".sha256");
                    if (string.IsNullOrEmpty(downloadUrl)) continue;

                    UpdateCheckResult result = new UpdateCheckResult();
                    result.UpdateAvailable = true;
                    result.Version = latest.ToString();
                    result.Tag = tag;
                    result.DownloadUrl = downloadUrl;
                    result.Sha256 = sha256;
                    result.ChecksumUrl = checksumUrl;
                    result.ReleasePageUrl = release.GetString("html_url");
                    result.Message = "YT Music Lite " + latest.ToString() + " is available.";
                    return result;
                }

                return UpdateCheckResult.NoUpdate("A newer release exists, but its Windows x64 update ZIP is missing.");
            }

            return UpdateCheckResult.NoUpdate("No YT Music Lite releases were found yet.");
        }

        public async Task DownloadAndInstallAsync(UpdateCheckResult update, Form owner)
        {
            if (update == null || !update.UpdateAvailable || string.IsNullOrEmpty(update.DownloadUrl))
            {
                throw new InvalidOperationException("No valid update is available.");
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "YTMusicLiteUpdate");
            Directory.CreateDirectory(tempRoot);
            string zipPath = Path.Combine(tempRoot, "YTMusicLite-" + update.Version + ".zip");

            await DownloadFileAsync(update.DownloadUrl, zipPath);

            string expectedSha256 = update.Sha256;
            if (string.IsNullOrEmpty(expectedSha256) && !string.IsNullOrEmpty(update.ChecksumUrl))
            {
                string checksumText = await DownloadStringAsync(update.ChecksumUrl);
                expectedSha256 = ParseChecksumFile(checksumText);
            }
            if (string.IsNullOrEmpty(expectedSha256))
            {
                try { File.Delete(zipPath); } catch { }
                throw new InvalidDataException("This release does not provide a SHA-256 digest. Update cancelled.");
            }

            string actual = ComputeSha256(zipPath);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(zipPath); } catch { }
                throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
            }

            string exePath = Application.ExecutablePath;
            string appDir = Path.GetDirectoryName(exePath);
            string updaterPath = Path.Combine(appDir, "YTMusicLite.Updater.exe");
            if (!File.Exists(updaterPath))
            {
                throw new FileNotFoundException("YTMusicLite.Updater.exe is missing. Rebuild YT Music Lite before using in-app updates.", updaterPath);
            }

            string tempUpdaterPath = Path.Combine(tempRoot, "YTMusicLite.Updater.exe");
            File.Copy(updaterPath, tempUpdaterPath, true);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = tempUpdaterPath;
            info.UseShellExecute = false;
            info.WorkingDirectory = appDir;
            info.Arguments = Quote(zipPath) + " " + Quote(appDir) + " " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + " " + Quote(exePath);
            Process.Start(info);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string clean = value.Trim();
            int dash = clean.IndexOf('-');
            if (dash >= 0) clean = clean.Substring(0, dash);
            return Version.TryParse(clean, out version);
        }

        private static string FindChecksumUrl(object[] assets, string expectedName)
        {
            if (assets == null) return "";
            foreach (object assetObject in assets)
            {
                DictionaryAdapter asset = new DictionaryAdapter(assetObject);
                if (string.Equals(asset.GetString("name"), expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return asset.GetString("browser_download_url");
                }
            }
            return "";
        }

        private static string ParseChecksumFile(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string trimmed = text.Trim();
            int space = trimmed.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' });
            string hash = space >= 0 ? trimmed.Substring(0, space) : trimmed;
            if (hash.Length != 64) return "";
            for (int i = 0; i < hash.Length; i++)
            {
                char c = hash[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return "";
            }
            return hash.ToLowerInvariant();
        }

        private static string ParseSha256Digest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest)) return "";
            const string prefix = "sha256:";
            if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "";
            string value = digest.Substring(prefix.Length).Trim();
            return value.Length == 64 ? value : "";
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static async Task<string> DownloadStringAsync(string url)
        {
            return await Task.Run(delegate
            {
                using (WebClient client = CreateClient())
                {
                    return client.DownloadString(url);
                }
            });
        }

        private static async Task DownloadFileAsync(string url, string path)
        {
            await Task.Run(delegate
            {
                using (WebClient client = CreateClient())
                {
                    client.DownloadFile(url, path);
                }
            });
        }

        private static WebClient CreateClient()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "YTMusicLite/" + CurrentVersion;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }

        private sealed class DictionaryAdapter
        {
            private readonly System.Collections.Generic.Dictionary<string, object> data;

            public DictionaryAdapter(object value)
            {
                data = value as System.Collections.Generic.Dictionary<string, object>;
            }

            public string GetString(string key)
            {
                if (data == null || !data.ContainsKey(key) || data[key] == null) return "";
                return Convert.ToString(data[key], CultureInfo.InvariantCulture);
            }

            public bool GetBool(string key)
            {
                if (data == null || !data.ContainsKey(key) || data[key] == null) return false;
                try { return Convert.ToBoolean(data[key], CultureInfo.InvariantCulture); }
                catch { return false; }
            }

            public object[] GetArray(string key)
            {
                if (data == null || !data.ContainsKey(key)) return null;
                return data[key] as object[];
            }
        }
    }

    public sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public string Version { get; set; }
        public string Tag { get; set; }
        public string DownloadUrl { get; set; }
        public string Sha256 { get; set; }
        public string ChecksumUrl { get; set; }
        public string ReleasePageUrl { get; set; }
        public string Message { get; set; }

        public static UpdateCheckResult NoUpdate(string message)
        {
            UpdateCheckResult result = new UpdateCheckResult();
            result.UpdateAvailable = false;
            result.Message = message;
            return result;
        }
    }
}
