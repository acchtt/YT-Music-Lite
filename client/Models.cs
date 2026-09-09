using System;
using System.Collections.Generic;

namespace YTMusicLite.Client
{
    internal sealed class Track
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Source { get; set; }
        public string ThumbnailUrl { get; set; }
        public double DurationSeconds { get; set; }
        public DateTime AddedUtc { get; set; }
        public DateTime LastPlayedUtc { get; set; }
        public int PlayCount { get; set; }

        public Track Clone()
        {
            return (Track)MemberwiseClone();
        }
    }

    internal sealed class Playlist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<Track> Tracks { get; set; }

        public Playlist()
        {
            Tracks = new List<Track>();
        }
    }

    internal sealed class LibraryData
    {
        public int SchemaVersion { get; set; }
        public List<Track> SavedTracks { get; set; }
        public List<Playlist> Playlists { get; set; }
        public List<Track> RecentTracks { get; set; }

        public LibraryData()
        {
            SchemaVersion = 2;
            SavedTracks = new List<Track>();
            Playlists = new List<Playlist>();
            RecentTracks = new List<Track>();
        }
    }

    internal enum AppPage
    {
        Home,
        Search,
        Library,
        Playlist,
        Queue,
        Settings
    }

    internal enum PlaybackState
    {
        Stopped,
        Resolving,
        Playing,
        Paused,
        Failed
    }

    internal sealed class PlaybackSnapshot : EventArgs
    {
        public PlaybackState State { get; set; }
        public Track Track { get; set; }
        public double PositionSeconds { get; set; }
        public double DurationSeconds { get; set; }
        public int Volume { get; set; }
        public string Error { get; set; }
    }
}
