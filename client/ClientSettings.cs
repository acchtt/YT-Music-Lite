using System;
using System.IO;
using System.Web.Script.Serialization;

namespace YTMusicLite.Client
{
    internal sealed class ClientSettings
    {
        public string CookieSource { get; set; }
        public string CookieFile { get; set; }

        public ClientSettings()
        {
            CookieSource = "none";
            CookieFile = "";
        }
    }

    internal sealed class SettingsStore
    {
        private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicLite", "settings.json");
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public ClientSettings Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    ClientSettings settings = serializer.Deserialize<ClientSettings>(File.ReadAllText(path));
                    if (settings != null) return settings;
                }
            }
            catch { }
            return new ClientSettings();
        }

        public void Save(ClientSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(settings));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }

    internal static class YtDlpOptions
    {
        public static string Authentication(ClientSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.CookieSource) || settings.CookieSource == "none") return "";
            if (settings.CookieSource == "file")
            {
                if (string.IsNullOrWhiteSpace(settings.CookieFile) || !File.Exists(settings.CookieFile)) return "";
                return " --cookies " + ProcessTools.Quote(settings.CookieFile);
            }
            if (settings.CookieSource == "edge" || settings.CookieSource == "brave" || settings.CookieSource == "chrome" || settings.CookieSource == "firefox") return " --cookies-from-browser " + settings.CookieSource;
            return "";
        }

        public static string FriendlyName(ClientSettings settings)
        {
            if (settings == null || settings.CookieSource == "none") return "Anonymous access";
            if (settings.CookieSource == "file") return "cookies.txt";
            return char.ToUpperInvariant(settings.CookieSource[0]) + settings.CookieSource.Substring(1) + " sign-in";
        }

        public static string ExplainFailure(string error, ClientSettings settings)
        {
            string value = (error ?? "").Trim();
            if (value.IndexOf("not a bot", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("cookies", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (settings == null || settings.CookieSource == "none") return "YouTube requested sign-in. Open Settings and choose Edge, Brave, Chrome, Firefox, or a cookies.txt file.";
                return "YouTube rejected the selected sign-in. Refresh its cookies in Settings, then try again.";
            }
            return value;
        }
    }
}
