using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace YTMusicLite.Client
{
    internal sealed class LibraryStore
    {
        private readonly string root;
        private readonly string dataPath;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public LibraryStore()
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicLite");
            dataPath = Path.Combine(root, "library-v2.json");
        }

        public LibraryData Load()
        {
            Directory.CreateDirectory(root);
            if (File.Exists(dataPath))
            {
                try
                {
                    LibraryData data = serializer.Deserialize<LibraryData>(File.ReadAllText(dataPath));
                    Normalize(data);
                    return data;
                }
                catch (Exception)
                {
                    string damaged = dataPath + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    try { File.Copy(dataPath, damaged, false); } catch { }
                }
            }

            LibraryData migrated = MigrateLegacy();
            Save(migrated);
            return migrated;
        }

        public void Save(LibraryData data)
        {
            Normalize(data);
            Directory.CreateDirectory(root);
            string temporary = dataPath + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(data));
            if (File.Exists(dataPath)) File.Replace(temporary, dataPath, dataPath + ".bak");
            else File.Move(temporary, dataPath);
        }

        public bool IsSaved(LibraryData data, Track track)
        {
            return data.SavedTracks.Any(item => SameTrack(item, track));
        }

        public void ToggleSaved(LibraryData data, Track track)
        {
            Track existing = data.SavedTracks.FirstOrDefault(item => SameTrack(item, track));
            if (existing != null) data.SavedTracks.Remove(existing);
            else
            {
                Track copy = track.Clone();
                if (copy.AddedUtc == DateTime.MinValue) copy.AddedUtc = DateTime.UtcNow;
                data.SavedTracks.Insert(0, copy);
            }
            Save(data);
        }

        public void RecordPlayed(LibraryData data, Track track)
        {
            track.LastPlayedUtc = DateTime.UtcNow;
            track.PlayCount++;
            Track previous = data.RecentTracks.FirstOrDefault(item => SameTrack(item, track));
            if (previous != null) data.RecentTracks.Remove(previous);
            data.RecentTracks.Insert(0, track.Clone());
            while (data.RecentTracks.Count > 24) data.RecentTracks.RemoveAt(data.RecentTracks.Count - 1);
            Save(data);
        }

        public Playlist CreatePlaylist(LibraryData data, string name)
        {
            Playlist playlist = new Playlist();
            playlist.Id = Guid.NewGuid().ToString("N");
            playlist.Name = name.Trim();
            playlist.CreatedUtc = DateTime.UtcNow;
            data.Playlists.Add(playlist);
            Save(data);
            return playlist;
        }

        public void AddToPlaylist(LibraryData data, Playlist playlist, Track track)
        {
            if (!playlist.Tracks.Any(item => SameTrack(item, track))) playlist.Tracks.Add(track.Clone());
            Save(data);
        }

        private LibraryData MigrateLegacy()
        {
            LibraryData result = new LibraryData();
            string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicLiteNative", "library.json");
            if (!File.Exists(legacy)) return result;
            try
            {
                LegacyCollection old = serializer.Deserialize<LegacyCollection>(File.ReadAllText(legacy));
                if (old == null) return result;
                if (old.Tracks != null) foreach (LegacyTrack track in old.Tracks) result.SavedTracks.Add(Convert(track));
                if (old.Playlists != null)
                {
                    foreach (KeyValuePair<string, List<LegacyTrack>> item in old.Playlists)
                    {
                        Playlist playlist = new Playlist();
                        playlist.Id = Guid.NewGuid().ToString("N");
                        playlist.Name = item.Key;
                        playlist.CreatedUtc = DateTime.UtcNow;
                        foreach (LegacyTrack track in item.Value ?? new List<LegacyTrack>()) playlist.Tracks.Add(Convert(track));
                        result.Playlists.Add(playlist);
                    }
                }
            }
            catch { }
            return result;
        }

        private static Track Convert(LegacyTrack track)
        {
            Track result = new Track();
            result.Id = TrackId(track.Source);
            result.Title = track.Title;
            result.Artist = track.Artist;
            result.Source = track.Source;
            result.AddedUtc = DateTime.UtcNow;
            return result;
        }

        private static void Normalize(LibraryData data)
        {
            if (data == null) throw new InvalidDataException("Library file is empty.");
            if (data.SavedTracks == null) data.SavedTracks = new List<Track>();
            if (data.Playlists == null) data.Playlists = new List<Playlist>();
            if (data.RecentTracks == null) data.RecentTracks = new List<Track>();
            if (data.DiscoveryTracks == null) data.DiscoveryTracks = new List<Track>();
            if (data.DiscoveryReason == null) data.DiscoveryReason = "";
            foreach (Track track in data.SavedTracks) YouTubeArtwork.Ensure(track);
            foreach (Track track in data.RecentTracks) YouTubeArtwork.Ensure(track);
            foreach (Track track in data.DiscoveryTracks) YouTubeArtwork.Ensure(track);
            foreach (Playlist playlist in data.Playlists)
            {
                if (playlist.Tracks == null) playlist.Tracks = new List<Track>();
                foreach (Track track in playlist.Tracks) YouTubeArtwork.Ensure(track);
            }
            data.SchemaVersion = 3;
        }

        public static bool SameTrack(Track left, Track right)
        {
            if (left == null || right == null) return false;
            if (!string.IsNullOrEmpty(left.Id) && !string.IsNullOrEmpty(right.Id)) return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
            return string.Equals(left.Source, right.Source, StringComparison.OrdinalIgnoreCase);
        }

        public static string TrackId(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return Guid.NewGuid().ToString("N");
            Uri uri;
            if (Uri.TryCreate(source, UriKind.Absolute, out uri))
            {
                string query = uri.Query.TrimStart('?');
                foreach (string part in query.Split('&')) if (part.StartsWith("v=", StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(part.Substring(2));
            }
            return source.ToLowerInvariant();
        }

        private sealed class LegacyTrack
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Source { get; set; }
        }

        private sealed class LegacyCollection
        {
            public List<LegacyTrack> Tracks { get; set; }
            public Dictionary<string, List<LegacyTrack>> Playlists { get; set; }
        }
    }
}
