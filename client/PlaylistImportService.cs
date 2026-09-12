using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace YTMusicLite.Client
{
    internal sealed class PlaylistImportService
    {
        private readonly ClientSettings settings;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public PlaylistImportService(ClientSettings clientSettings)
        {
            settings = clientSettings ?? new ClientSettings();
        }

        public async Task<Playlist> ImportAsync(string url)
        {
            Uri source = Validate(url);
            string runtime = ProcessTools.Find("deno");
            string arguments = "--ignore-config --js-runtimes " + ProcessTools.Quote("deno:" + runtime) + YtDlpOptions.Authentication(settings) + " --yes-playlist --flat-playlist --dump-single-json --no-warnings --playlist-end 250 --socket-timeout 20 --retries 1 -- " + ProcessTools.Quote(source.AbsoluteUri);
            using (Process process = ProcessTools.Start(ProcessTools.Find("yt-dlp"), arguments, true))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                Task exited = Task.Run(delegate { process.WaitForExit(); });
                if (await Task.WhenAny(exited, Task.Delay(90000)) != exited)
                {
                    ProcessTools.KillTree(process);
                    throw new TimeoutException("Playlist import took too long.");
                }
                await exited;
                string json = await output;
                string error = await errors;
                if (process.ExitCode != 0) throw new InvalidOperationException("Playlist import failed. " + YtDlpOptions.ExplainFailure(Short(error), settings));
                return Parse(json, source.AbsoluteUri);
            }
        }

        internal Playlist Parse(string json, string sourceUrl)
        {
            Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json ?? "");
            if (data == null) throw new InvalidOperationException("YouTube returned an empty playlist.");
            string externalId = First(data, "id", "playlist_id");
            if (string.IsNullOrWhiteSpace(externalId)) externalId = PlaylistId(sourceUrl);
            Playlist result = new Playlist
            {
                Id = "youtube:" + externalId,
                Name = First(data, "title", "playlist_title"),
                SourceUrl = sourceUrl,
                IsRemote = true,
                CreatedUtc = DateTime.UtcNow,
                LastSyncedUtc = DateTime.UtcNow
            };
            if (string.IsNullOrWhiteSpace(result.Name)) result.Name = "YouTube playlist";
            object raw;
            IEnumerable entries = data.TryGetValue("entries", out raw) ? raw as IEnumerable : null;
            if (entries != null) foreach (object entry in entries)
            {
                Dictionary<string, object> item = entry as Dictionary<string, object>;
                if (item == null) continue;
                string id = First(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                result.Tracks.Add(new Track
                {
                    Id = id,
                    Title = First(item, "title", "fulltitle"),
                    Artist = First(item, "artist", "channel", "uploader"),
                    Source = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id),
                    ThumbnailUrl = CatalogService.Thumbnail(item, id),
                    DurationSeconds = Number(item, "duration"),
                    AddedUtc = DateTime.UtcNow
                });
            }
            if (result.Tracks.Count == 0) throw new InvalidOperationException("No playable songs were found in this playlist.");
            return result;
        }

        internal static Uri Validate(string value)
        {
            Uri uri;
            if (!Uri.TryCreate((value ?? "").Trim(), UriKind.Absolute, out uri)) throw new ArgumentException("Paste a complete YouTube playlist URL.");
            string host = uri.Host.ToLowerInvariant();
            bool youtube = host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com");
            if (!youtube || string.IsNullOrWhiteSpace(PlaylistId(uri.AbsoluteUri))) throw new ArgumentException("The URL must contain a YouTube playlist ID.");
            return uri;
        }

        private static string PlaylistId(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return "";
            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                int equals = part.IndexOf('=');
                if (equals > 0 && string.Equals(part.Substring(0, equals), "list", StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(part.Substring(equals + 1));
            }
            return "";
        }

        private static string First(Dictionary<string, object> data, params string[] keys)
        {
            foreach (string key in keys)
            {
                object raw;
                string value = data.TryGetValue(key, out raw) && raw != null ? Convert.ToString(raw, CultureInfo.InvariantCulture) : "";
                if (!string.IsNullOrWhiteSpace(value) && value != "NA") return value;
            }
            return "";
        }

        private static double Number(Dictionary<string, object> data, string key)
        {
            double value;
            return double.TryParse(First(data, key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static string Short(string value)
        {
            value = (value ?? "").Trim();
            return value.Length <= 300 ? value : value.Substring(0, 300);
        }
    }
}
