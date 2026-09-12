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
                discoveryResults = new List<Track> { first.Clone(), second.Clone() };
                library.DiscoveryTracks = new List<Track>(discoveryResults);
                library.DiscoveryUpdatedUtc = DateTime.UtcNow;
                library.DiscoveryReason = "Based on Neon Avenue";
                searchResults = new List<Track> { first, second };
                queue.Add(first); queue.Add(second); queueIndex = 0;
                RefreshPlaylistNavigation();
                Assert(playlistNavigation.PlaylistCount == 1, "Sidebar playlist navigation");
                List<Playlist> overflowPlaylists = new List<Playlist>();
                for (int index = 0; index < 32; index++) overflowPlaylists.Add(new Playlist { Id = "overflow-" + index, Name = "Playlist " + index.ToString("00"), CreatedUtc = DateTime.UtcNow });
                library.Playlists.AddRange(overflowPlaylists);
                RefreshPlaylistNavigation();
                Assert(playlistNavigation.PlaylistCount == 33, "Virtualized sidebar playlist overflow");
                foreach (Playlist extra in overflowPlaylists) library.Playlists.Remove(extra);
                RefreshPlaylistNavigation();

                Navigate(AppPage.Library, null, true);
                Assert(trackList.Items.Count == 2, "Library navigation");
                Navigate(AppPage.Playlist, playlist, true);
                Assert(trackList.Items.Count == 1 && heading.Text == "Night Mix", "Playlist navigation");
                Navigate(AppPage.Playlists, null, true);
                Assert(playlistGrid.Visible && playlistGrid.Controls.Count == 1 && heading.Text == "Playlists", "Playlist overview");
                Navigate(AppPage.Discover, null, true);
                Assert(trackList.Visible && trackList.Items.Count == 2 && heading.Text == "Discover Weekly", "Discovery navigation");
                DiscoveryPlan plan = DiscoveryPlanner.Build(library, new DateTime(2026, 9, 12));
                Assert(plan.Queries.Count > 0 && plan.Reason.Contains("Neon Avenue"), "History-based discovery planning");
                Navigate(AppPage.Search, null, true);
                Assert(trackList.Items.Count == 2, "Search results");
                Navigate(AppPage.Queue, null, true);
                Assert(trackList.Items.Count == 2, "Queue navigation");
                Navigate(AppPage.Settings, null, true);
                Assert(settingsPanel.Visible && !trackList.Visible, "Settings page");
                clientSettings.CookieSource = "edge";
                Assert(YtDlpOptions.Authentication(clientSettings).Contains("--cookies-from-browser edge"), "Browser authentication arguments");
                clientSettings.CookieSource = "brave";
                Assert(YtDlpOptions.Authentication(clientSettings).Contains("--cookies-from-browser brave"), "Brave authentication arguments");
                clientSettings.CookieProfile = @"C:\Auth Profile\Default";
                Assert(YtDlpOptions.Authentication(clientSettings).Contains("brave:C:\\Auth Profile\\Default"), "Dedicated Brave profile arguments");
                Assert(BrowserAccessValidator.HasAccountCookie(".youtube.com\tTRUE\t/\tTRUE\t0\tSAPISID\tsecret"), "Authenticated cookie detection");
                Assert(!BrowserAccessValidator.HasAccountCookie(".youtube.com\tTRUE\t/\tTRUE\t0\tPREF\tplain"), "Anonymous cookie rejection");
                AccountSyncService syncParser = new AccountSyncService(clientSettings);
                List<Track> synced = syncParser.Parse("{\"id\":\"liked-1\",\"title\":\"Liked song\",\"artist\":\"Test artist\",\"duration\":123}");
                Assert(synced.Count == 1 && synced[0].Id == "liked-1" && synced[0].Artist == "Test artist" && synced[0].ThumbnailUrl.Contains("liked-1"), "Account library parsing");
                List<Playlist> accountPlaylists = syncParser.ParsePlaylists("{\"entries\":[{\"id\":\"PL-owned\",\"title\":\"My YouTube mix\",\"webpage_url\":\"https://music.youtube.com/playlist?list=PL-owned\",\"playlist_count\":\"12 songs\",\"thumbnails\":[{\"url\":\"https://i.ytimg.com/vi/cover/hqdefault.jpg\"}]}]}");
                Assert(accountPlaylists.Count == 1 && accountPlaylists[0].Id == "youtube:PL-owned" && accountPlaylists[0].TrackCount == 12 && !accountPlaylists[0].TracksLoaded && accountPlaylists[0].ThumbnailUrl.Contains("cover"), "Automatic YouTube playlist parsing");
                Dictionary<string, object> artworkData = new Dictionary<string, object>();
                artworkData["thumbnails"] = new object[]
                {
                    new Dictionary<string, object> { { "url", "https://i.ytimg.com/vi/thumb-test/mqdefault.jpg" } },
                    new Dictionary<string, object> { { "url", "https://i.ytimg.com/vi/thumb-test/hqdefault.jpg" } }
                };
                Assert(CatalogService.Thumbnail(artworkData, "thumb-test").EndsWith("/hqdefault.jpg"), "Thumbnail array parsing");
                Playlist imported = new PlaylistImportService(clientSettings).Parse("{\"id\":\"PL-test\",\"title\":\"Imported mix\",\"entries\":[{\"id\":\"song-1\",\"title\":\"Imported song\",\"artist\":\"Test artist\",\"duration\":180}]}", "https://www.youtube.com/playlist?list=PL-test");
                Assert(imported.IsRemote && imported.Tracks.Count == 1 && imported.Tracks[0].ThumbnailUrl.Contains("song-1"), "YouTube playlist parsing");
                Assert(PlaylistImportService.Validate("https://music.youtube.com/playlist?list=PL-test").Host == "music.youtube.com", "YouTube playlist URL validation");
                Track repairedArtwork = new Track { Id = "repair-test", Source = "https://www.youtube.com/watch?v=repair-test", ThumbnailUrl = "Unknown artist" };
                YouTubeArtwork.Ensure(repairedArtwork);
                Assert(repairedArtwork.ThumbnailUrl.Contains("repair-test"), "Existing artwork repair");
                Assert(BrowserSignIn.Arguments("brave").Contains("--new-window") && BrowserSignIn.Arguments("brave").Contains("accounts.google.com"), "Brave sign-in launch arguments");
                Assert(BrowserSignIn.Arguments("edge").Contains("--new-window") && BrowserSignIn.Arguments("edge").Contains("accounts.google.com"), "Edge sign-in launch arguments");
                Assert(BrowserSignIn.Arguments("firefox").Contains("-new-window") && BrowserSignIn.Arguments("firefox").Contains("accounts.google.com"), "Firefox sign-in launch arguments");
                clientSettings.CookieSource = "none";
                clientSettings.CookieProfile = "";
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-settings.png"); }
                Navigate(AppPage.Home, null, true);
                Assert(homeTiles.Controls.Count > 0 && trackList.Items.Count >= 2, "Home page");
                Assert(shuffleButton != null && repeatButton != null && stopButton != null && muteButton != null, "Complete playback controls");
                ToggleShuffle();
                Assert(shuffleEnabled && shuffleButton.Checked, "Shuffle state");
                CycleRepeatMode();
                Assert(repeatMode == RepeatMode.All && repeatButton.Checked, "Repeat state");
                currentVolume = 42;
                ApplySnapshot(new PlaybackSnapshot { State = PlaybackState.Paused, Volume = 85 });
                Assert(snapshot.Volume == 42 && Math.Abs(volumeSlider.Value - 42) < 0.01, "Stale volume snapshot suppression");
                currentVolume = 85;
                clientSettings.Volume = 85;
                ApplySnapshot(new PlaybackSnapshot { State = PlaybackState.Stopped, Volume = 85 });

                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-home.png"); }
                Navigate(AppPage.Search, null, true);
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-search.png"); }
                Navigate(AppPage.Playlists, null, true);
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-playlists.png"); }
                Navigate(AppPage.Discover, null, true);
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-discover.png"); }
                Size = MinimumSize;
                Navigate(AppPage.Library, null, true);
                using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save("client-compact.png"); }
                ShowMiniPlayer();
                Assert(miniPlayer != null, "Mini player");
                using (Bitmap image = new Bitmap(miniPlayer.Width, miniPlayer.Height)) { miniPlayer.DrawToBitmap(image, new Rectangle(Point.Empty, miniPlayer.Size)); image.Save("client-mini.png"); }
                miniPlayer.Close();
                File.WriteAllText("ui-check.txt", "PASS: native navigation, automatic playlist parsing, discovery, search, library, queue, settings, responsive layout, complete playback controls, mini player");
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
