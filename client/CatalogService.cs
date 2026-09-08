using System;
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

        public async Task<List<Track>> SearchAsync(string query)
        {
            Cancel();
            if (string.IsNullOrWhiteSpace(query)) return new List<Track>();
            string runtime = ProcessTools.Find("deno");
            string arguments = "--ignore-config --js-runtimes " + ProcessTools.Quote("deno:" + runtime) + " --flat-playlist --dump-json --no-warnings --socket-timeout 15 --retries 1 -- " + ProcessTools.Quote("ytsearch30:" + query.Trim());
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
            if (process.ExitCode != 0) throw new InvalidOperationException("YouTube search failed. " + Trim(error, 180));

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
                    track.ThumbnailUrl = First(data, "thumbnail");
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
