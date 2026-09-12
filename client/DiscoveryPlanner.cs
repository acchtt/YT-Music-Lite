using System;
using System.Collections.Generic;
using System.Linq;

namespace YTMusicLite.Client
{
    internal sealed class DiscoveryPlan
    {
        public string Reason { get; set; }
        public List<string> Queries { get; set; }

        public DiscoveryPlan()
        {
            Reason = "";
            Queries = new List<string>();
        }
    }

    internal static class DiscoveryPlanner
    {
        public static DiscoveryPlan Build(LibraryData library, DateTime utc)
        {
            Dictionary<string, int> scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int weight = 80;
            foreach (Track track in (library == null ? new List<Track>() : library.RecentTracks).Take(20))
            {
                string artist = Artist(track);
                if (!string.IsNullOrEmpty(artist)) scores[artist] = Score(scores, artist) + weight + Math.Max(0, track.PlayCount * 5);
                weight = Math.Max(10, weight - 3);
            }
            foreach (Track track in library == null ? new List<Track>() : library.SavedTracks)
            {
                string artist = Artist(track);
                if (!string.IsNullOrEmpty(artist)) scores[artist] = Score(scores, artist) + 20 + Math.Max(0, track.PlayCount * 3);
            }

            List<string> artists = scores.OrderByDescending(item => item.Value).ThenBy(item => item.Key).Select(item => item.Key).Take(8).ToList();
            DiscoveryPlan result = new DiscoveryPlan();
            if (artists.Count == 0)
            {
                result.Reason = "Fresh releases and trending music";
                result.Queries.Add("new music " + utc.Year + " official audio");
                result.Queries.Add("trending music official audio");
                return result;
            }

            int week = Math.Max(0, (int)((utc.Date - new DateTime(2024, 1, 1)).TotalDays / 7));
            string first = artists[week % artists.Count];
            string second = artists[(week + Math.Max(1, artists.Count / 2)) % artists.Count];
            result.Reason = artists.Count == 1 ? "Based on " + first : "Based on " + first + " and " + second;
            result.Queries.Add(first + " music official audio");
            if (!string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) result.Queries.Add(second + " music official audio");
            return result;
        }

        private static int Score(Dictionary<string, int> values, string key)
        {
            int value;
            return values.TryGetValue(key, out value) ? value : 0;
        }

        private static string Artist(Track track)
        {
            string value = track == null ? "" : (track.Artist ?? "").Trim();
            if (string.Equals(value, "Unknown artist", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Local audio", StringComparison.OrdinalIgnoreCase)) return "";
            return value;
        }
    }
}
