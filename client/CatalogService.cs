using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace YTMusicLite.Client
{
    internal sealed class CatalogService : IDisposable
    {
        private Process active;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly ClientSettings settings;

        public CatalogService() : this(new SettingsStore().Load()) { }
        public CatalogService(ClientSettings clientSettings) { settings = clientSettings ?? new ClientSettings(); }

        public async Task<List<Track>> SearchAsync(string query)
        {
            Cancel();
            if (string.IsNullOrWhiteSpace(query)) return new List<Track>();
            string runtime = ProcessTools.Find("deno");
            string arguments = "--ignore-config --js-runtimes " + ProcessTools.Quote("deno:" + runtime) + YtDlpOptions.Authentication(settings) + " --flat-playlist --dump-json --no-warnings --socket-timeout 15 --retries 1 -- " + ProcessTools.Quote("ytsearch30:" + query.Trim());
            Process process = ProcessTools.Start(ProcessTools.Find("yt-dlp"), arguments, true);
            active = process;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            Task exited = Task.Run(delegate { process.WaitForExit(); });
            if (await Task.WhenAny(exited, Task.Delay(45000)) != exited)
            {
                ProcessTools.KillTree(process);
                throw new TimeoutException("Search took too long. Check your connection and try again.");
            }
            await exited;
            string json = await output;
            string error = await errors;
            if (active == process) active = null;
            int exitCode = process.ExitCode;
            process.Dispose();
            if (exitCode != 0) throw new InvalidOperationException("YouTube search failed. " + YtDlpOptions.ExplainFailure(Trim(error, 500), settings));

            List<Track> results = new List<Track>();
            foreach (string line in json.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(line);
                    string id = Value(data, "id");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    Track track = new Track();
                    track.Id = id;
                    track.Title = Value(data, "title");
                    track.Artist = First(data, "channel", "uploader", "artist");
                    track.Source = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id);
                    track.ThumbnailUrl = Thumbnail(data, id);
                    track.DurationSeconds = Number(data, "duration");
                    results.Add(track);
                }
                catch { }
            }
            return results;
        }

        public void Cancel()
        {
            Process process = active;
            active = null;
            if (process != null)
            {
                ProcessTools.KillTree(process);
                process.Dispose();
            }
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

        internal static string Thumbnail(Dictionary<string, object> data, string id)
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

        private static string Trim(string value, int length)
        {
            value = (value ?? "").Trim();
            return value.Length <= length ? value : value.Substring(0, length);
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
