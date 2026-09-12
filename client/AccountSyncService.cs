using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace YTMusicLite.Client
{
    internal sealed class AccountSyncService
    {
        private readonly ClientSettings settings;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public AccountSyncService(ClientSettings clientSettings)
        {
            settings = clientSettings;
        }

        public async Task<List<Track>> LoadLikedSongsAsync()
        {
            if (settings == null || !settings.AccessVerified) throw new InvalidOperationException("Sign in and verify your account before syncing.");
            string[] urls = { "https://music.youtube.com/playlist?list=LM", "https://www.youtube.com/playlist?list=LM" };
            string lastError = "";
            foreach (string url in urls)
            {
                SyncResult result = await LoadAsync(url);
                if (result.Tracks.Count > 0 || result.Success) return result.Tracks;
                lastError = result.Error;
            }
            throw new InvalidOperationException("Your liked songs could not be loaded. " + YtDlpOptions.ExplainFailure(Short(lastError), settings));
        }

        private async Task<SyncResult> LoadAsync(string url)
        {
            string arguments = "--ignore-config" + YtDlpOptions.Authentication(settings) + " --flat-playlist --dump-json --no-warnings --playlist-end 250 --socket-timeout 20 --retries 1 -- " + ProcessTools.Quote(url);
            using (Process process = ProcessTools.Start(ProcessTools.Find("yt-dlp"), arguments, true))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                Task exited = Task.Run(delegate { process.WaitForExit(); });
                if (await Task.WhenAny(exited, Task.Delay(90000)) != exited)
                {
                    ProcessTools.KillTree(process);
                    return new SyncResult { Error = "Account sync timed out." };
                }
                await exited;
                string json = await output;
                string error = await errors;
                List<Track> tracks = Parse(json);
                return new SyncResult { Success = process.ExitCode == 0, Tracks = tracks, Error = error };
            }
        }

        internal List<Track> Parse(string json)
        {
            List<Track> tracks = new List<Track>();
            foreach (string line in (json ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(line);
                    string id = Value(data, "id");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    Track track = new Track
                    {
                        Id = id,
                        Title = First(data, "title", "fulltitle"),
                        Artist = First(data, "artist", "channel", "uploader"),
                        Source = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id),
                        ThumbnailUrl = Thumbnail(data, id),
                        DurationSeconds = Number(data, "duration"),
                        AddedUtc = DateTime.UtcNow
                    };
                    tracks.Add(track);
                }
                catch { }
            }
            return tracks;
        }

        private static string First(Dictionary<string, object> data, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value = Value(data, key);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return "Unknown artist";
        }

        private static string Value(Dictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }

        private static double Number(Dictionary<string, object> data, string key)
        {
            double number;
            return double.TryParse(Value(data, key), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
        }

        private static string Thumbnail(Dictionary<string, object> data, string id)
        {
            string direct = Value(data, "thumbnail");
            Uri parsed;
            if (Uri.TryCreate(direct, UriKind.Absolute, out parsed)) return direct;
            object raw;
            string best = "";
            if (data.TryGetValue("thumbnails", out raw))
            {
                IEnumerable entries = raw as IEnumerable;
                if (entries != null) foreach (object entry in entries)
                {
                    Dictionary<string, object> item = entry as Dictionary<string, object>;
                    if (item == null) continue;
                    string url = Value(item, "url");
                    if (Uri.TryCreate(url, UriKind.Absolute, out parsed)) best = url;
                }
            }
            return string.IsNullOrEmpty(best) ? YouTubeArtwork.ForVideo(id) : best;
        }

        private static string Short(string value)
        {
            value = (value ?? "").Trim();
            return value.Length <= 240 ? value : value.Substring(0, 240);
        }

        private sealed class SyncResult
        {
            public bool Success;
            public List<Track> Tracks = new List<Track>();
            public string Error = "";
        }
    }
}
