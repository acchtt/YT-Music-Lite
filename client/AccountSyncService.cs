using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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

        public async Task<List<Playlist>> LoadPlaylistsAsync()
        {
            if (settings == null || !settings.AccessVerified) throw new InvalidOperationException("Sign in and verify your account before syncing.");
            string[] urls =
            {
                "https://music.youtube.com/browse/FEmusic_liked_playlists",
                "https://music.youtube.com/library/playlists",
                "https://www.youtube.com/feed/playlists"
            };
            string lastError = "";
            bool reachedLibrary = false;
            foreach (string url in urls)
            {
                PlaylistSyncResult result = await LoadPlaylistIndexAsync(url);
                if (result.Success) reachedLibrary = true;
                if (result.Playlists.Count > 0) return result.Playlists;
                lastError = result.Error;
            }
            if (reachedLibrary) return new List<Playlist>();
            throw new InvalidOperationException("Your playlists could not be loaded. " + YtDlpOptions.ExplainFailure(Short(lastError), settings));
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

        private async Task<PlaylistSyncResult> LoadPlaylistIndexAsync(string url)
        {
            string runtime = ProcessTools.Find("deno");
            string arguments = "--ignore-config --js-runtimes " + ProcessTools.Quote("deno:" + runtime) + YtDlpOptions.Authentication(settings) + " --flat-playlist --dump-single-json --no-warnings --playlist-end 100 --socket-timeout 20 --retries 1 -- " + ProcessTools.Quote(url);
            using (Process process = ProcessTools.Start(ProcessTools.Find("yt-dlp"), arguments, true))
            {
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                Task exited = Task.Run(delegate { process.WaitForExit(); });
                if (await Task.WhenAny(exited, Task.Delay(90000)) != exited)
                {
                    ProcessTools.KillTree(process);
                    return new PlaylistSyncResult { Error = "Playlist sync timed out." };
                }
                await exited;
                string json = await output;
                string error = await errors;
                return new PlaylistSyncResult { Success = process.ExitCode == 0, Playlists = ParsePlaylists(json), Error = error };
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

        internal List<Playlist> ParsePlaylists(string json)
        {
            List<Playlist> playlists = new List<Playlist>();
            try
            {
                Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json ?? "");
                object raw;
                IEnumerable entries = data != null && data.TryGetValue("entries", out raw) ? raw as IEnumerable : null;
                if (entries == null) return playlists;
                foreach (object entry in entries)
                {
                    Dictionary<string, object> item = entry as Dictionary<string, object>;
                    if (item == null) continue;
                    string sourceUrl = Optional(item, "webpage_url", "url");
                    string externalId = PlaylistId(sourceUrl);
                    if (string.IsNullOrWhiteSpace(externalId)) externalId = Optional(item, "playlist_id", "id");
                    if (externalId.StartsWith("VL", StringComparison.Ordinal) && externalId.Length > 2) externalId = externalId.Substring(2);
                    if (string.IsNullOrWhiteSpace(externalId) || string.Equals(externalId, "LM", StringComparison.OrdinalIgnoreCase)) continue;
                    sourceUrl = "https://music.youtube.com/playlist?list=" + Uri.EscapeDataString(externalId);
                    Playlist playlist = new Playlist
                    {
                        Id = "youtube:" + externalId,
                        Name = Optional(item, "title", "playlist_title"),
                        SourceUrl = sourceUrl,
                        ThumbnailUrl = Image(item),
                        TrackCount = Count(item),
                        TracksLoaded = false,
                        IsRemote = true,
                        CreatedUtc = DateTime.UtcNow,
                        LastSyncedUtc = DateTime.UtcNow
                    };
                    if (string.IsNullOrWhiteSpace(playlist.Name)) playlist.Name = "YouTube playlist";
                    if (!playlists.Any(existing => string.Equals(existing.Id, playlist.Id, StringComparison.OrdinalIgnoreCase))) playlists.Add(playlist);
                }
            }
            catch { }
            return playlists;
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

        private static string Optional(Dictionary<string, object> data, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value = Value(data, key);
                if (!string.IsNullOrWhiteSpace(value) && value != "NA") return value;
            }
            return "";
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

        private static string Image(Dictionary<string, object> data)
        {
            string direct = Value(data, "thumbnail");
            if (IsWebUrl(direct)) return direct;
            object raw;
            string best = "";
            if (data.TryGetValue("thumbnails", out raw))
            {
                IEnumerable entries = raw as IEnumerable;
                if (entries != null) foreach (object entry in entries)
                {
                    Dictionary<string, object> item = entry as Dictionary<string, object>;
                    string url = item == null ? "" : Value(item, "url");
                    if (IsWebUrl(url)) best = url;
                }
            }
            return best;
        }

        private static int Count(Dictionary<string, object> data)
        {
            string value = Optional(data, "playlist_count", "count", "n_entries");
            string digits = new string((value ?? "").Where(char.IsDigit).ToArray());
            int count;
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) ? count : 0;
        }

        private static string PlaylistId(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return "";
            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                int equals = part.IndexOf('=');
                if (equals > 0 && string.Equals(part.Substring(0, equals), "list", StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(part.Substring(equals + 1));
            }
            string marker = "/browse/";
            int browse = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            return browse < 0 ? "" : Uri.UnescapeDataString(uri.AbsolutePath.Substring(browse + marker.Length));
        }

        private static bool IsWebUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
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

        private sealed class PlaylistSyncResult
        {
            public bool Success;
            public List<Playlist> Playlists = new List<Playlist>();
            public string Error = "";
        }
    }
}
