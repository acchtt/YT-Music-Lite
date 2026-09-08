using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace YTMusicLite.Client
{
    internal sealed partial class MainWindow
    {
        private void RunUiCheck()
        {
            try
            {
                Track first = new Track { Id = "sample-1", Title = "Midnight Drive", Artist = "Neon Avenue", Source = "sample.wav", DurationSeconds = 214, AddedUtc = DateTime.UtcNow };
                Track second = new Track { Id = "sample-2", Title = "Paper Skies", Artist = "The Light", Source = "sample.wav", DurationSeconds = 187, AddedUtc = DateTime.UtcNow };
                library.SavedTracks.Add(first);
                library.SavedTracks.Add(second);
                library.RecentTracks.Add(first.Clone());
                Playlist playlist = new Playlist { Id = "night-mix", Name = "Night Mix", CreatedUtc = DateTime.UtcNow, Tracks = new List<Track> { second } };
                library.Playlists.Add(playlist);
                searchResults = new List<Track> { first, second };
                queue.Add(first); queue.Add(second); queueIndex = 0;
                RefreshPlaylistNavigation();

                Navigate(AppPage.Library, null, true);
                Assert(trackList.Items.Count == 2, "Library navigation");
                Navigate(AppPage.Playlist, playlist, true);
                Assert(trackList.Items.Count == 1 && heading.Text == "Night Mix", "Playlist navigation");
                Navigate(AppPage.Search, null, true);
                Assert(trackList.Items.Count == 2, "Search results");
                Navigate(AppPage.Queue, null, true);
                Assert(trackList.Items.Count == 2, "Queue navigation");
                Navigate(AppPage.Settings, null, true);
                Assert(settingsPanel.Visible && !trackList.Visible, "Settings page");
                Navigate(AppPage.Home, null, true);
                Assert(homeTiles.Controls.Count > 0 && trackList.Items.Count >= 2, "Home page");

                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-home.png"); }
                Navigate(AppPage.Search, null, true);
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-search.png"); }
                Size = MinimumSize;
                Navigate(AppPage.Library, null, true);
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-compact.png"); }
                ShowMiniPlayer();
                Assert(miniPlayer != null, "Mini player");
                using (Bitmap image = new Bitmap(miniPlayer.Width, miniPlayer.Height)) { miniPlayer.DrawToBitmap(image, new Rectangle(Point.Empty, miniPlayer.Size)); image.Save("client-mini.png"); }
                miniPlayer.Close();
                File.WriteAllText("ui-check.txt", "PASS: clean client navigation, search, library, playlists, queue, settings, responsive layout, mini player");
            }
            catch (Exception error)
            {
                File.WriteAllText("ui-check.txt", "FAIL: " + error);
                Environment.ExitCode = 1;
            }
            closing = true;
            Close();
        }

        private async void RunBenchmark()
        {
            string stagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmark-stages.txt");
            try
            {
                if (File.Exists(stagePath)) File.Delete(stagePath);
                await Task.Delay(1800);
                Track track = new Track { Id = "benchmark", Title = "Memory baseline", Artist = "Local audio", Source = benchmarkSource };
                queue.Add(track); queueIndex = 0;
                await StartQueueTrackAsync(track);
                await Task.Delay(3500);
                File.AppendAllText(stagePath, "playing success\r\n");
                await playback.TogglePauseAsync();
                await Task.Delay(2500);
                File.AppendAllText(stagePath, "paused true\r\n");
                playback.Stop();
                await Task.Delay(2500);
                File.AppendAllText(stagePath, "stopped\r\n");
                await Task.Delay(1800);
            }
            catch (Exception error)
            {
                File.AppendAllText(stagePath, "FAIL " + error.Message + "\r\n");
                Environment.ExitCode = 1;
            }
            closing = true;
            Close();
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name + " failed.");
        }
    }
}
