using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed partial class MainWindow
    {
        private void Navigate(AppPage page, Playlist playlist, bool addHistory)
        {
            currentPage = page;
            currentPlaylist = playlist;
            if (addHistory)
            {
                if (historyIndex < history.Count - 1) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(new PageTarget { Page = page, PlaylistId = playlist == null ? null : playlist.Id });
                historyIndex = history.Count - 1;
            }
            foreach (KeyValuePair<AppPage, NavButton> item in navigationButtons) { item.Value.Selected = item.Key == page; item.Value.Invalidate(); }
            playlistNavigation.SelectedPlaylistId = page == AppPage.Playlist && playlist != null ? playlist.Id : null;
            backButton.Enabled = historyIndex > 0;
            forwardButton.Enabled = historyIndex >= 0 && historyIndex < history.Count - 1;
            RenderPage();
            if (page == AppPage.Playlist && playlist != null && playlist.IsRemote && !playlist.TracksLoaded && !loadingPlaylists.Contains(playlist.Id))
            {
                BeginInvoke((Action)delegate { EnsurePlaylistLoaded(playlist); });
            }
        }

        private void MoveHistory(int offset)
        {
            int next = historyIndex + offset;
            if (next < 0 || next >= history.Count) return;
            historyIndex = next;
            PageTarget target = history[next];
            Playlist playlist = string.IsNullOrEmpty(target.PlaylistId) ? null : library.Playlists.FirstOrDefault(item => item.Id == target.PlaylistId);
            Navigate(target.Page, playlist, false);
        }

        private void RenderPage()
        {
            actionBar.Controls.Clear();
            homeTiles.Controls.Clear();
            trackList.EmptyTitle = "Nothing here yet";
            trackList.EmptyBody = "Search for music or import audio from this computer.";
            settingsPanel.Visible = currentPage == AppPage.Settings;
            playlistGrid.Visible = currentPage == AppPage.Playlists;
            trackList.Visible = currentPage != AppPage.Settings && currentPage != AppPage.Playlists;
            homeRow.Height = currentPage == AppPage.Home ? 96 : 0;

            List<Track> visible = VisibleTracks();
            switch (currentPage)
            {
                case AppPage.Home:
                    heading.Text = Greeting();
                    subtitle.Text = library.RecentTracks.Count > 0 ? "Pick up where you left off" : "Your music, without a browser running in the background";
                    AddAction("Play", AppIcon.Play, true, async delegate { await PlayFirstAsync(); });
                    AddAction("Discover", AppIcon.Discover, false, delegate { Navigate(AppPage.Discover, null, true); });
                    AddAction("Import audio", AppIcon.Add, false, ImportAudio);
                    PopulateHomeTiles(visible);
                    break;
                case AppPage.Discover:
                    heading.Text = "Discover Weekly";
                    subtitle.Text = discovering ? "Building your weekly mix…" : (string.IsNullOrWhiteSpace(library.DiscoveryReason) ? "Recommendations shaped by your listening history" : library.DiscoveryReason);
                    trackList.EmptyTitle = discovering ? "Building your mix…" : "No recommendations yet";
                    trackList.EmptyBody = discovering ? "Using your recent plays and saved artists." : "Play or save a few songs, then refresh this page.";
                    AddTrackActions(false);
                    AddAction("Refresh mix", AppIcon.Discover, false, RefreshDiscovery);
                    break;
                case AppPage.Search:
                    heading.Text = "Search";
                    subtitle.Text = searching ? "Searching YouTube Music…" : (searchResults.Count == 0 ? "Find songs, artists, and performances" : searchResults.Count + " results");
                    AddTrackActions(false);
                    break;
                case AppPage.Library:
                    heading.Text = "Your library";
                    subtitle.Text = visible.Count == 0 ? "Songs you save appear here" : visible.Count + (visible.Count == 1 ? " saved song" : " saved songs");
                    AddTrackActions(true);
                    AddAction("Import audio", AppIcon.Add, false, ImportAudio);
                    break;
                case AppPage.Playlists:
                    heading.Text = "Playlists";
                    subtitle.Text = library.Playlists.Count == 0 ? "Your YouTube playlists appear automatically after sign-in" : library.Playlists.Count + (library.Playlists.Count == 1 ? " playlist" : " playlists");
                    AddAction("Create playlist", AppIcon.Add, true, CreatePlaylist);
                    AddAction("Import YouTube", AppIcon.Download, false, ImportYouTubePlaylist);
                    PopulatePlaylistGrid();
                    break;
                case AppPage.Playlist:
                    heading.Text = currentPlaylist == null ? "Playlist" : currentPlaylist.Name;
                    subtitle.Text = currentPlaylist != null && currentPlaylist.IsRemote && !currentPlaylist.TracksLoaded ? "Loading songs from YouTube…" : visible.Count + (visible.Count == 1 ? " song" : " songs") + (currentPlaylist != null && currentPlaylist.IsRemote ? " · YouTube snapshot" : "");
                    trackList.EmptyTitle = "This playlist is empty";
                    trackList.EmptyBody = currentPlaylist != null && currentPlaylist.IsRemote && !currentPlaylist.TracksLoaded ? "Fetching this playlist only when you open it keeps startup fast and memory low." : "Use Add to playlist from Search, Discover, or Your library.";
                    AddTrackActions(true);
                    AddAction("Playlist options", AppIcon.More, false, ShowCurrentPlaylistMenu);
                    break;
                case AppPage.Queue:
                    heading.Text = "Queue";
                    subtitle.Text = visible.Count == 0 ? "Songs you add will line up here" : "Up next · " + visible.Count + (visible.Count == 1 ? " song" : " songs");
                    AddTrackActions(true);
                    AddAction("Clear queue", AppIcon.Close, false, ClearQueue);
                    break;
                case AppPage.Settings:
                    heading.Text = "Settings";
                    subtitle.Text = "Playback, memory, and updates";
                    break;
            }
            if (currentPage != AppPage.Playlists) trackList.SetTracks(visible);
            UpdateActionState();
            if (currentPage == AppPage.Search && !searching) searchBox.Focus();
            if (currentPage == AppPage.Discover && !discovering && !discoveryAutoAttempted && (discoveryResults.Count == 0 || library.DiscoveryUpdatedUtc < DateTime.UtcNow.AddDays(-7)))
            {
                discoveryAutoAttempted = true;
                BeginInvoke((Action)RefreshDiscovery);
            }
        }

        private List<Track> VisibleTracks()
        {
            if (currentPage == AppPage.Search) return searchResults;
            if (currentPage == AppPage.Discover) return discoveryResults;
            if (currentPage == AppPage.Library) return library.SavedTracks;
            if (currentPage == AppPage.Playlist) return currentPlaylist == null ? new List<Track>() : currentPlaylist.Tracks;
            if (currentPage == AppPage.Queue) return queue;
            if (currentPage == AppPage.Home)
            {
                List<Track> combined = new List<Track>();
                foreach (Track track in library.RecentTracks.Concat(library.SavedTracks)) if (!combined.Any(item => LibraryStore.SameTrack(item, track))) combined.Add(track);
                return combined;
            }
            return new List<Track>();
        }

        private void PopulatePlaylistGrid()
        {
            playlistGrid.Controls.Clear();
            foreach (Playlist playlist in library.Playlists.OrderByDescending(item => item.LastSyncedUtc).ThenBy(item => item.Name))
            {
                Playlist target = playlist;
                PlaylistCard card = new PlaylistCard { Playlist = target, Margin = new Padding(0, 0, 14, 14), AccessibleName = "Open playlist " + target.Name };
                card.Click += delegate { Navigate(AppPage.Playlist, target, true); };
                playlistGrid.Controls.Add(card);
            }
            if (playlistGrid.Controls.Count == 0)
            {
                Label empty = new Label { Text = "No playlists yet. Create one or import a YouTube playlist URL.", ForeColor = Theme.Muted, AutoSize = true, Padding = new Padding(0, 24, 0, 0), Margin = Padding.Empty };
                playlistGrid.Controls.Add(empty);
            }
        }

        private void PopulateHomeTiles(List<Track> tracks)
        {
            foreach (Track track in tracks.Take(4))
            {
                Track target = track;
                HomeTile tile = new HomeTile { Track = target, Margin = new Padding(0, 0, 12, 0), AccessibleName = "Play " + target.Title };
                tile.Click += async delegate { await PlayTrackAsync(target, tracks.IndexOf(target), tracks); };
                homeTiles.Controls.Add(tile);
            }
            if (homeTiles.Controls.Count == 0)
            {
                Label empty = new Label { Text = "Search for a song to start listening.", ForeColor = Theme.Muted, AutoSize = true, Padding = new Padding(0, 28, 0, 0) };
                homeTiles.Controls.Add(empty);
            }
        }

        private void AddTrackActions(bool allowRemove)
        {
            AddAction("Play", AppIcon.Play, true, async delegate { await PlaySelectedAsync(); });
            AddAction("Add to queue", AppIcon.Queue, false, AddSelectedToQueue);
            AddAction("Save", AppIcon.Heart, false, ToggleSaveSelected);
            AddAction("Add to playlist", AppIcon.Playlist, false, ShowPlaylistMenuForSelected);
            if (allowRemove) AddAction("Remove", AppIcon.Close, false, RemoveSelected);
        }

        private void AddAction(string label, AppIcon icon, bool primary, Action action)
        {
            PillButton button = new PillButton { Label = label, Icon = icon, ShowIcon = true, Primary = primary, AutoSize = false, Width = Math.Max(96, TextRenderer.MeasureText(label, Font).Width + 50), Margin = new Padding(0, 0, 9, 0), AccessibleName = label, Tag = label };
            button.Click += delegate { action(); };
            actionBar.Controls.Add(button);
        }

        private async Task SearchAsync()
        {
            string query = searchBox.Text.Trim();
            if (searching || string.IsNullOrWhiteSpace(query)) { if (string.IsNullOrWhiteSpace(query)) { Navigate(AppPage.Search, null, true); searchBox.Focus(); } return; }
            searching = true;
            Navigate(AppPage.Search, null, currentPage != AppPage.Search);
            subtitle.Text = "Searching YouTube Music…";
            statusLabel.Text = "Searching…";
            try
            {
                searchResults = await catalog.SearchAsync(query);
                statusLabel.Text = searchResults.Count == 0 ? "No results" : "Search complete";
            }
            catch (Exception error)
            {
                searchResults.Clear();
                statusLabel.Text = error.Message;
            }
            finally
            {
                searching = false;
                if (!closing && currentPage == AppPage.Search) RenderPage();
            }
        }

        private async void RefreshDiscovery()
        {
            if (discovering) return;
            discovering = true;
            if (currentPage == AppPage.Discover) RenderPage();
            statusLabel.Text = "Building your weekly mix…";
            try
            {
                DiscoveryPlan plan = DiscoveryPlanner.Build(library, DateTime.UtcNow);
                List<Track> found = new List<Track>();
                foreach (string query in plan.Queries)
                {
                    List<Track> batch = await catalog.SearchAsync(query, 14);
                    foreach (Track track in batch)
                    {
                        if (found.Any(item => LibraryStore.SameTrack(item, track))) continue;
                        if (library.SavedTracks.Any(item => LibraryStore.SameTrack(item, track))) continue;
                        if (library.RecentTracks.Any(item => LibraryStore.SameTrack(item, track))) continue;
                        found.Add(track);
                    }
                }
                discoveryResults = found.Take(24).ToList();
                library.DiscoveryTracks = new List<Track>(discoveryResults.Select(item => item.Clone()));
                library.DiscoveryUpdatedUtc = DateTime.UtcNow;
                library.DiscoveryReason = plan.Reason;
                if (!automation) store.Save(library);
                statusLabel.Text = discoveryResults.Count == 0 ? "No new recommendations found" : "Your weekly mix is ready";
            }
            catch (Exception error)
            {
                statusLabel.Text = "Discovery failed: " + error.Message;
            }
            finally
            {
                discovering = false;
                if (!closing && currentPage == AppPage.Discover) RenderPage();
            }
        }

        private async Task PlayFirstAsync()
        {
            List<Track> items = VisibleTracks();
            if (items.Count == 0) { statusLabel.Text = "Search for a song first"; return; }
            await PlayTrackAsync(items[0], 0, items);
        }

        private async Task PlaySelectedAsync()
        {
            Track track = trackList.SelectedTrack;
            if (track == null) { statusLabel.Text = "Select a song first"; return; }
            List<Track> visible = VisibleTracks();
            await PlayTrackAsync(track, trackList.SelectedIndex, visible);
        }

        private async Task PlayTrackAsync(Track track, int index, IList<Track> context)
        {
            queue.Clear();
            if (context != null) queue.AddRange(context);
            if (queue.Count == 0) queue.Add(track);
            queueIndex = Math.Max(0, Math.Min(queue.Count - 1, index));
            if (shuffleEnabled && queue.Count > 1)
            {
                Track selected = queue[queueIndex];
                queue.RemoveAt(queueIndex);
                ShuffleRange(0);
                queue.Insert(0, selected);
                queueIndex = 0;
            }
            await StartQueueTrackAsync(track);
        }

        private async Task StartQueueTrackAsync(Track track)
        {
            playerTitle.Text = track.Title ?? "Untitled";
            playerArtist.Text = string.IsNullOrWhiteSpace(track.Artist) ? "Unknown artist" : track.Artist;
            playerArtwork.KeyText = track.Title;
            playerArtwork.ArtworkUrl = track.ThumbnailUrl;
            statusLabel.Text = "Preparing audio…";
            saveButton.Checked = store.IsSaved(library, track);
            saveButton.Invalidate();
            if (!automation) { try { store.RecordPlayed(library, track); } catch { } }
            try { await playback.PlayAsync(track, silentPlayback); }
            catch (Exception error) { if (!closing) statusLabel.Text = error.Message; }
        }

        private async Task TogglePlaybackAsync()
        {
            if (snapshot.State == PlaybackState.Playing || snapshot.State == PlaybackState.Paused)
            {
                try { await playback.TogglePauseAsync(); } catch (Exception error) { statusLabel.Text = error.Message; }
                return;
            }
            if (queueIndex >= 0 && queueIndex < queue.Count) await StartQueueTrackAsync(queue[queueIndex]);
            else if (trackList.SelectedTrack != null) await PlaySelectedAsync();
            else await PlayFirstAsync();
        }

        private async Task MoveQueueAsync(int offset)
        {
            if (queue.Count == 0) { statusLabel.Text = "The queue is empty"; return; }
            if (offset < 0 && snapshot.PositionSeconds > 3 && (snapshot.State == PlaybackState.Playing || snapshot.State == PlaybackState.Paused))
            {
                try { await playback.SeekAsync(0); } catch (Exception error) { statusLabel.Text = error.Message; }
                return;
            }
            int next = queueIndex + offset;
            if (next < 0 || next >= queue.Count)
            {
                if (repeatMode == RepeatMode.All) next = next < 0 ? queue.Count - 1 : 0;
                else { statusLabel.Text = offset > 0 ? "End of queue" : "Start of queue"; return; }
            }
            queueIndex = next;
            await StartQueueTrackAsync(queue[queueIndex]);
            if (currentPage == AppPage.Queue) RenderPage();
        }

        private void ToggleShuffle()
        {
            shuffleEnabled = !shuffleEnabled;
            if (shuffleEnabled && queue.Count > 1) ShuffleRange(Math.Max(0, queueIndex + 1));
            clientSettings.Shuffle = shuffleEnabled;
            SavePlaybackSettings();
            ApplyPlaybackOptions();
            statusLabel.Text = shuffleEnabled ? "Shuffle on" : "Shuffle off";
            if (currentPage == AppPage.Queue) RenderPage();
        }

        private void ShuffleRange(int start)
        {
            for (int index = queue.Count - 1; index > start; index--)
            {
                int swap = random.Next(start, index + 1);
                Track value = queue[index];
                queue[index] = queue[swap];
                queue[swap] = value;
            }
        }

        private void CycleRepeatMode()
        {
            repeatMode = repeatMode == RepeatMode.Off ? RepeatMode.All : (repeatMode == RepeatMode.All ? RepeatMode.One : RepeatMode.Off);
            clientSettings.RepeatMode = repeatMode == RepeatMode.One ? "one" : (repeatMode == RepeatMode.All ? "all" : "off");
            SavePlaybackSettings();
            ApplyPlaybackOptions();
            statusLabel.Text = repeatMode == RepeatMode.One ? "Repeat one" : (repeatMode == RepeatMode.All ? "Repeat all" : "Repeat off");
        }

        private void StopPlayback()
        {
            playback.Stop();
            statusLabel.Text = "Stopped";
        }

        private async Task SetVolumeAsync(int value)
        {
            int adjusted = Math.Max(0, Math.Min(100, value));
            if (adjusted > 0) lastAudibleVolume = adjusted;
            currentVolume = adjusted;
            snapshot.Volume = adjusted;
            clientSettings.Volume = adjusted;
            SavePlaybackSettings();
            try { await playback.SetVolumeAsync(adjusted); }
            catch (Exception error) { statusLabel.Text = error.Message; }
        }

        private async Task ToggleMuteAsync()
        {
            int target = currentVolume > 0 ? 0 : Math.Max(1, lastAudibleVolume);
            volumeSlider.Value = target;
            await SetVolumeAsync(target);
        }

        private async Task AdjustVolumeAsync(int change)
        {
            int target = Math.Max(0, Math.Min(100, currentVolume + change));
            volumeSlider.Value = target;
            await SetVolumeAsync(target);
        }

        private void SavePlaybackSettings()
        {
            if (!automation) settingsStore.Save(clientSettings);
        }

        private void ApplyPlaybackOptions()
        {
            if (shuffleButton != null) { shuffleButton.Checked = shuffleEnabled; shuffleButton.AccessibleName = shuffleEnabled ? "Turn shuffle off" : "Turn shuffle on"; shuffleButton.Invalidate(); }
            if (repeatButton != null)
            {
                repeatButton.Checked = repeatMode != RepeatMode.Off;
                repeatButton.Icon = repeatMode == RepeatMode.One ? AppIcon.RepeatOne : AppIcon.Repeat;
                repeatButton.AccessibleName = repeatMode == RepeatMode.One ? "Repeat one" : (repeatMode == RepeatMode.All ? "Repeat all" : "Repeat off");
                repeatButton.Invalidate();
            }
            if (miniPlayer != null) miniPlayer.ApplyPlaybackOptions(shuffleEnabled, repeatMode);
        }

        private static RepeatMode ParseRepeatMode(string value)
        {
            if (string.Equals(value, "one", StringComparison.OrdinalIgnoreCase)) return RepeatMode.One;
            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)) return RepeatMode.All;
            return RepeatMode.Off;
        }

        private void AddSelectedToQueue()
        {
            Track track = trackList.SelectedTrack;
            if (track == null) { statusLabel.Text = "Select a song first"; return; }
            queue.Add(track);
            statusLabel.Text = "Added to queue";
            if (currentPage == AppPage.Queue) RenderPage();
        }

        private void ClearQueue()
        {
            queue.Clear();
            queueIndex = -1;
            if (currentPage == AppPage.Queue) RenderPage();
            statusLabel.Text = "Queue cleared";
        }

        private void ToggleSaveSelected()
        {
            Track track = trackList.SelectedTrack;
            if (track == null) { statusLabel.Text = "Select a song first"; return; }
            ToggleSaved(track);
        }

        private void ToggleSaveCurrent()
        {
            if (snapshot.Track == null) { statusLabel.Text = "Nothing is playing"; return; }
            ToggleSaved(snapshot.Track);
        }

        private void ToggleSaved(Track track)
        {
            if (!automation) store.ToggleSaved(library, track);
            else
            {
                Track existing = library.SavedTracks.FirstOrDefault(item => LibraryStore.SameTrack(item, track));
                if (existing == null) library.SavedTracks.Insert(0, track.Clone()); else library.SavedTracks.Remove(existing);
            }
            bool saved = store.IsSaved(library, track);
            statusLabel.Text = saved ? "Saved to your library" : "Removed from your library";
            saveButton.Checked = saved;
            saveButton.Invalidate();
            if (currentPage == AppPage.Library || currentPage == AppPage.Home) RenderPage();
        }

        private void CreatePlaylist()
        {
            string name = Prompt("Create playlist", "Playlist name");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (library.Playlists.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) { statusLabel.Text = "A playlist with that name already exists"; return; }
            Playlist playlist;
            if (automation)
            {
                playlist = new Playlist { Id = Guid.NewGuid().ToString("N"), Name = name, CreatedUtc = DateTime.UtcNow };
                library.Playlists.Add(playlist);
            }
            else playlist = store.CreatePlaylist(library, name);
            RefreshPlaylistNavigation();
            Navigate(AppPage.Playlist, playlist, true);
        }

        private async void ImportYouTubePlaylist()
        {
            string url = Prompt("Import YouTube playlist", "Paste playlist URL", "Import");
            if (string.IsNullOrWhiteSpace(url)) return;
            statusLabel.Text = "Importing YouTube playlist…";
            try
            {
                Playlist imported = await new PlaylistImportService(clientSettings).ImportAsync(url);
                Playlist playlist = UpsertPlaylist(imported);
                statusLabel.Text = "Imported " + playlist.Tracks.Count + " songs";
                Navigate(AppPage.Playlist, playlist, true);
            }
            catch (Exception error)
            {
                statusLabel.Text = error.Message;
                MessageBox.Show(this, error.Message, "Playlist import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void RefreshCurrentPlaylist()
        {
            Playlist target = currentPlaylist;
            if (target == null || !target.IsRemote || string.IsNullOrWhiteSpace(target.SourceUrl)) return;
            statusLabel.Text = "Refreshing " + target.Name + "…";
            try
            {
                Playlist imported = await new PlaylistImportService(clientSettings).ImportAsync(target.SourceUrl);
                string localName = target.Name;
                target.Tracks = imported.Tracks;
                target.SourceUrl = imported.SourceUrl;
                target.LastSyncedUtc = imported.LastSyncedUtc;
                target.IsRemote = true;
                target.Name = localName;
                target.ThumbnailUrl = imported.ThumbnailUrl;
                target.TrackCount = imported.Tracks.Count;
                target.TracksLoaded = true;
                if (!automation) store.Save(library);
                RefreshPlaylistNavigation();
                statusLabel.Text = "Playlist refreshed";
                if (!closing && currentPlaylist == target) RenderPage();
            }
            catch (Exception error)
            {
                statusLabel.Text = "Playlist refresh failed: " + error.Message;
            }
        }

        private async void EnsurePlaylistLoaded(Playlist target)
        {
            if (target == null || target.TracksLoaded || string.IsNullOrWhiteSpace(target.SourceUrl) || !loadingPlaylists.Add(target.Id)) return;
            statusLabel.Text = "Loading " + target.Name + "…";
            try
            {
                Playlist imported = await new PlaylistImportService(clientSettings).ImportAsync(target.SourceUrl);
                target.Tracks = imported.Tracks;
                target.SourceUrl = imported.SourceUrl;
                target.ThumbnailUrl = imported.ThumbnailUrl;
                target.TrackCount = imported.Tracks.Count;
                target.TracksLoaded = true;
                target.LastSyncedUtc = imported.LastSyncedUtc;
                if (!automation) store.Save(library);
                statusLabel.Text = "Loaded " + target.Tracks.Count + " songs from " + target.Name;
                RefreshPlaylistNavigation();
                if (!closing && currentPlaylist == target) RenderPage();
            }
            catch (Exception error)
            {
                statusLabel.Text = "Playlist load failed: " + error.Message;
                if (!closing && currentPlaylist == target)
                {
                    trackList.EmptyTitle = "Could not load this playlist";
                    trackList.EmptyBody = error.Message;
                    trackList.Invalidate();
                }
            }
            finally
            {
                loadingPlaylists.Remove(target.Id);
            }
        }

        private Playlist UpsertPlaylist(Playlist imported)
        {
            Playlist existing = library.Playlists.FirstOrDefault(item => string.Equals(item.Id, imported.Id, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(item.SourceUrl) && string.Equals(item.SourceUrl, imported.SourceUrl, StringComparison.OrdinalIgnoreCase)));
            if (existing == null)
            {
                library.Playlists.Add(imported);
                existing = imported;
            }
            else
            {
                existing.Name = imported.Name;
                existing.SourceUrl = imported.SourceUrl;
                existing.IsRemote = true;
                existing.LastSyncedUtc = imported.LastSyncedUtc;
                existing.Tracks = imported.Tracks;
                existing.ThumbnailUrl = imported.ThumbnailUrl;
                existing.TrackCount = imported.Tracks.Count;
                existing.TracksLoaded = true;
            }
            if (!automation) store.Save(library);
            RefreshPlaylistNavigation();
            return existing;
        }

        private void RenameCurrentPlaylist()
        {
            if (currentPlaylist == null) return;
            string name = Prompt("Rename playlist", currentPlaylist.Name, "Save");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (library.Playlists.Any(item => item != currentPlaylist && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) { statusLabel.Text = "A playlist with that name already exists"; return; }
            currentPlaylist.Name = name.Trim();
            if (!automation) store.Save(library);
            RefreshPlaylistNavigation();
            RenderPage();
            statusLabel.Text = "Playlist renamed";
        }

        private void DeleteCurrentPlaylist()
        {
            Playlist target = currentPlaylist;
            if (target == null) return;
            if (!automation && MessageBox.Show(this, "Delete \"" + target.Name + "\" from this PC?", "Delete playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            library.Playlists.Remove(target);
            if (!automation) store.Save(library);
            RefreshPlaylistNavigation();
            statusLabel.Text = "Playlist deleted";
            Navigate(AppPage.Playlists, null, true);
        }

        private void ShowCurrentPlaylistMenu()
        {
            if (currentPlaylist == null) return;
            ContextMenuStrip menu = NewMenu();
            if (currentPlaylist.IsRemote) menu.Items.Add("Refresh from YouTube", null, delegate { RefreshCurrentPlaylist(); });
            menu.Items.Add("Rename", null, delegate { RenameCurrentPlaylist(); });
            menu.Items.Add("Delete from this PC", null, delegate { DeleteCurrentPlaylist(); });
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        private void RefreshPlaylistNavigation()
        {
            playlistNavigation.SetPlaylists(library.Playlists.OrderBy(item => item.Name));
            playlistNavigation.SelectedPlaylistId = currentPage == AppPage.Playlist && currentPlaylist != null ? currentPlaylist.Id : null;
        }

        private void ShowPlaylistMenuForSelected()
        {
            Track track = trackList.SelectedTrack;
            if (track == null) { statusLabel.Text = "Select a song first"; return; }
            ShowPlaylistMenu(track, Cursor.Position);
        }

        private void ShowPlaylistMenu(Track track, Point screenLocation)
        {
            ContextMenuStrip menu = NewMenu();
            if (library.Playlists.Count == 0) menu.Items.Add("Create a playlist first", null, delegate { CreatePlaylist(); });
            else foreach (Playlist playlist in library.Playlists.OrderBy(item => item.Name))
            {
                Playlist target = playlist;
                menu.Items.Add(target.Name, null, delegate
                {
                    if (automation) { if (!target.Tracks.Any(item => LibraryStore.SameTrack(item, track))) target.Tracks.Add(track.Clone()); }
                    else store.AddToPlaylist(library, target, track);
                    statusLabel.Text = "Added to " + target.Name;
                });
            }
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(screenLocation);
        }

        private void ShowTrackMenu()
        {
            Track track = trackList.SelectedTrack;
            if (track == null) return;
            ContextMenuStrip menu = NewMenu();
            menu.Items.Add("Play", null, async delegate { await PlaySelectedAsync(); });
            menu.Items.Add("Add to queue", null, delegate { AddSelectedToQueue(); });
            menu.Items.Add(store.IsSaved(library, track) ? "Remove from library" : "Save to library", null, delegate { ToggleSaved(track); });
            ToolStripMenuItem playlistItem = new ToolStripMenuItem("Add to playlist");
            playlistItem.BackColor = Theme.Surface; playlistItem.ForeColor = Theme.Text;
            if (library.Playlists.Count == 0) playlistItem.DropDownItems.Add("Create playlist…", null, delegate { CreatePlaylist(); });
            foreach (Playlist playlist in library.Playlists.OrderBy(item => item.Name))
            {
                Playlist target = playlist;
                playlistItem.DropDownItems.Add(target.Name, null, delegate { if (!automation) store.AddToPlaylist(library, target, track); else target.Tracks.Add(track.Clone()); });
            }
            menu.Items.Add(playlistItem);
            if (currentPage == AppPage.Library || currentPage == AppPage.Playlist || currentPage == AppPage.Queue) menu.Items.Add("Remove", null, delegate { RemoveSelected(); });
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        private ContextMenuStrip NewMenu()
        {
            return new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false, Font = new Font("Segoe UI", 9) };
        }

        private void RemoveSelected()
        {
            int index = trackList.SelectedIndex;
            if (index < 0) { statusLabel.Text = "Select a song first"; return; }
            if (currentPage == AppPage.Library) library.SavedTracks.RemoveAt(index);
            else if (currentPage == AppPage.Playlist && currentPlaylist != null) currentPlaylist.Tracks.RemoveAt(index);
            else if (currentPage == AppPage.Queue)
            {
                queue.RemoveAt(index);
                if (index < queueIndex) queueIndex--;
                else if (index == queueIndex && queueIndex >= queue.Count) queueIndex = queue.Count - 1;
            }
            else return;
            if (!automation && currentPage != AppPage.Queue) store.Save(library);
            RenderPage();
            statusLabel.Text = "Removed";
        }

        private void ImportAudio()
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Multiselect = true, Filter = "Audio files|*.mp3;*.m4a;*.ogg;*.opus;*.wav;*.flac|All files|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                foreach (string file in dialog.FileNames)
                {
                    if (library.SavedTracks.Any(item => string.Equals(item.Source, file, StringComparison.OrdinalIgnoreCase))) continue;
                    library.SavedTracks.Insert(0, new Track { Id = LibraryStore.TrackId(file), Title = Path.GetFileNameWithoutExtension(file), Artist = "Local audio", Source = file, AddedUtc = DateTime.UtcNow });
                }
                if (!automation) store.Save(library);
                Navigate(AppPage.Library, null, true);
            }
        }

        private string Prompt(string title, string placeholder)
        {
            return Prompt(title, placeholder, "Create");
        }

        private string Prompt(string title, string placeholder, string confirmLabel)
        {
            using (Form dialog = new Form { Text = title, Size = new Size(420, 175), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, BackColor = Theme.Window, ForeColor = Theme.Text, Font = Font })
            {
                TextBox field = new TextBox { Left = 20, Top = 24, Width = 364, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Text = placeholder };
                PillButton confirm = new PillButton { Left = 266, Top = 70, Width = 118, Label = confirmLabel, Primary = true };
                Button accept = new Button { Visible = false, DialogResult = DialogResult.OK };
                confirm.Click += delegate { dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                dialog.Controls.Add(field); dialog.Controls.Add(confirm); dialog.Controls.Add(accept); dialog.AcceptButton = accept; field.SelectAll();
                return dialog.ShowDialog(this) == DialogResult.OK ? field.Text.Trim() : null;
            }
        }

        private void UpdateActionState()
        {
            Track selected = trackList.SelectedTrack;
            foreach (Control control in actionBar.Controls)
            {
                string label = Convert.ToString(control.Tag);
                bool pageAction = label == "Import audio" || label == "Clear queue" || label == "Discover" || label == "Refresh mix" || label == "Create playlist" || label == "Import YouTube" || label == "Playlist options";
                control.Enabled = selected != null || pageAction;
            }
        }

        private void PlaybackSnapshotChanged(object sender, PlaybackSnapshot update)
        {
            if (closing) return;
            try { BeginInvoke((Action)(delegate { ApplySnapshot(update); })); } catch { }
        }

        private void ApplySnapshot(PlaybackSnapshot update)
        {
            update.Volume = currentVolume;
            snapshot = update;
            playPauseButton.Icon = update.State == PlaybackState.Playing ? AppIcon.Pause : AppIcon.Play;
            playPauseButton.Invalidate();
            if (update.Track != null)
            {
                playerTitle.Text = update.Track.Title ?? "Untitled";
                playerArtist.Text = string.IsNullOrWhiteSpace(update.Track.Artist) ? "Unknown artist" : update.Track.Artist;
                playerArtwork.KeyText = update.Track.Title;
                playerArtwork.ArtworkUrl = update.Track.ThumbnailUrl;
                saveButton.Checked = store.IsSaved(library, update.Track);
                saveButton.Invalidate();
            }
            if (update.State == PlaybackState.Resolving) statusLabel.Text = "Preparing audio…";
            else if (update.State == PlaybackState.Playing) statusLabel.Text = "Playing";
            else if (update.State == PlaybackState.Paused) statusLabel.Text = "Paused";
            else if (update.State == PlaybackState.Stopped) statusLabel.Text = "Stopped";
            else if (update.State == PlaybackState.Failed) statusLabel.Text = update.Error ?? "Playback failed";
            double duration = update.DurationSeconds > 0 ? update.DurationSeconds : (update.Track == null ? 0 : update.Track.DurationSeconds);
            updatingProgress = true;
            progressSlider.Maximum = Math.Max(1, duration);
            if (!progressSlider.IsDragging) progressSlider.Value = Math.Min(progressSlider.Maximum, update.PositionSeconds);
            elapsedLabel.Text = FormatTime(update.PositionSeconds);
            durationLabel.Text = FormatTime(duration);
            updatingProgress = false;
            if (!volumeSlider.IsDragging) volumeSlider.Value = currentVolume;
            if (update.Volume > 0) lastAudibleVolume = update.Volume;
            muteButton.Icon = update.Volume == 0 ? AppIcon.VolumeMuted : AppIcon.Volume;
            muteButton.AccessibleName = update.Volume == 0 ? "Unmute" : "Mute";
            muteButton.Invalidate();
            if (miniPlayer != null) miniPlayer.ApplySnapshot(update);
        }

        private void PlaybackEnded(object sender, EventArgs eventArgs)
        {
            if (closing) return;
            try
            {
                BeginInvoke((Action)(async delegate
                {
                    if (repeatMode == RepeatMode.One && queueIndex >= 0 && queueIndex < queue.Count) await StartQueueTrackAsync(queue[queueIndex]);
                    else await MoveQueueAsync(1);
                }));
            }
            catch { }
        }

        private void ShowMiniPlayer()
        {
            if (miniPlayer != null && !miniPlayer.IsDisposed) { miniPlayer.Activate(); return; }
            miniPlayer = new MiniPlayerWindow(
                playback,
                async delegate { await MoveQueueAsync(-1); },
                async delegate { await TogglePlaybackAsync(); },
                async delegate { await MoveQueueAsync(1); },
                delegate { StopPlayback(); },
                delegate { ToggleShuffle(); },
                delegate { CycleRepeatMode(); },
                async delegate(int value) { await SetVolumeAsync(value); },
                async delegate { await ToggleMuteAsync(); },
                delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            miniPlayer.FormClosed += delegate { miniPlayer = null; };
            miniPlayer.ApplyPlaybackOptions(shuffleEnabled, repeatMode);
            miniPlayer.ApplySnapshot(snapshot);
            miniPlayer.Show(this);
        }

        private async Task CheckForUpdatesAsync()
        {
            statusLabel.Text = "Checking for updates…";
            try
            {
                UpdateService service = new UpdateService();
                UpdateCheckResult result = await service.CheckAsync();
                if (!result.UpdateAvailable) { statusLabel.Text = result.Message; return; }
                if (MessageBox.Show(this, result.Message + "\n\nDownload and install it now?", "YT Music Lite update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) { statusLabel.Text = "Update available"; return; }
                Progress<int> progress = new Progress<int>(delegate(int value) { statusLabel.Text = "Downloading update… " + value + "%"; });
                PreparedUpdate prepared = await service.PrepareAsync(result, progress);
                service.InstallPrepared(prepared);
                closing = true;
                Close();
            }
            catch (Exception error) { statusLabel.Text = "Update failed: " + error.Message; }
        }

        private async void SetBrowserAccess(string browser)
        {
            if (string.Equals(browser, "brave", StringComparison.OrdinalIgnoreCase))
            {
                await SignInWithBraveAsync();
                return;
            }
            string error = BrowserSignIn.Open(browser);
            if (!string.IsNullOrEmpty(error))
            {
                statusLabel.Text = error;
                MessageBox.Show(this, error, "YouTube sign-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            clientSettings.CookieSource = browser;
            clientSettings.CookieFile = "";
            clientSettings.CookieProfile = "";
            clientSettings.AccessVerified = false;
            SaveAccessSettings();
            MessageBox.Show(this,
                "A YouTube Music sign-in window has opened.\n\nFinish signing in, then close the browser so YT Music Lite can securely read that session when you play a song.",
                "Finish signing in",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async Task SignInWithBraveAsync()
        {
            string error;
            BrowserSignInSession session = BrowserSignIn.OpenDedicatedBrave(out error);
            if (session == null)
            {
                statusLabel.Text = error;
                MessageBox.Show(this, error, "YouTube sign-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            clientSettings.CookieSource = "brave";
            clientSettings.CookieFile = "";
            clientSettings.CookieProfile = session.ProfileDirectory;
            clientSettings.AccessVerified = false;
            SaveAccessSettings();
            youtubeAccessTitle.Text = "Waiting for Brave sign-in";
            youtubeAccessBody.Text = "Finish signing in inside the Brave window, then close that window. Verification will continue automatically.";
            statusLabel.Text = "Waiting for Brave sign-in…";
            try { await Task.Run(delegate { session.Process.WaitForExit(); }); }
            catch (Exception waitError)
            {
                session.Process.Dispose();
                if (closing || IsDisposed) return;
                MessageBox.Show(this, "Could not finish Brave sign-in: " + waitError.Message, "Sign-in failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            session.Process.Dispose();
            if (closing || IsDisposed) return;
            statusLabel.Text = "Verifying Brave session…";
            youtubeAccessTitle.Text = "Verifying sign-in…";
            error = await BrowserAccessValidator.ValidateAsync(clientSettings);
            if (string.IsNullOrEmpty(error))
            {
                clientSettings.AccessVerified = true;
                SaveAccessSettings();
                await SyncAccountAsync(true);
            }
            else
            {
                clientSettings.AccessVerified = false;
                SaveAccessSettings();
                statusLabel.Text = error;
                youtubeAccessTitle.Text = "Brave sign-in failed";
                youtubeAccessBody.Text = error;
                MessageBox.Show(this, error, "Sign-in was not completed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void SyncAccount()
        {
            await SyncAccountAsync(false, false);
        }

        private async Task SyncAccountAsync(bool afterSignIn)
        {
            await SyncAccountAsync(afterSignIn, false);
        }

        private async Task SyncAccountAsync(bool afterSignIn, bool quiet)
        {
            if (accountSyncing) return;
            if (!clientSettings.AccessVerified)
            {
                string message = "Sign in with Brave and wait for Connected before syncing your library.";
                statusLabel.Text = message;
                if (!quiet) MessageBox.Show(this, message, "Account sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            accountSyncing = true;
            statusLabel.Text = "Syncing your YouTube playlists…";
            youtubeAccessBody.Text = "Connected. Loading playlists and liked songs from YouTube Music…";
            try
            {
                AccountSyncService service = new AccountSyncService(clientSettings);
                List<Playlist> remotePlaylists = null;
                List<Track> tracks = null;
                Exception playlistError = null;
                Exception likedError = null;
                try { remotePlaylists = await service.LoadPlaylistsAsync(); }
                catch (Exception error) { playlistError = error; }
                try { tracks = await service.LoadLikedSongsAsync(); }
                catch (Exception error) { likedError = error; }
                if (remotePlaylists == null && tracks == null) throw playlistError ?? likedError ?? new InvalidOperationException("YouTube Music did not return your library.");

                int playlistCount = 0;
                foreach (Playlist remote in remotePlaylists ?? new List<Playlist>())
                {
                    Playlist existing = library.Playlists.FirstOrDefault(item => string.Equals(item.Id, remote.Id, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        library.Playlists.Add(remote);
                        existing = remote;
                    }
                    else
                    {
                        existing.SourceUrl = remote.SourceUrl;
                        existing.IsRemote = true;
                        existing.LastSyncedUtc = remote.LastSyncedUtc;
                        if (!string.IsNullOrWhiteSpace(remote.ThumbnailUrl)) existing.ThumbnailUrl = remote.ThumbnailUrl;
                        existing.TrackCount = Math.Max(remote.TrackCount, existing.Tracks == null ? 0 : existing.Tracks.Count);
                    }
                    playlistCount++;
                }

                int added = 0;
                foreach (Track track in tracks ?? new List<Track>())
                {
                    if (library.SavedTracks.Any(item => LibraryStore.SameTrack(item, track))) continue;
                    library.SavedTracks.Add(track);
                    added++;
                }
                if (tracks != null)
                {
                    Playlist liked = library.Playlists.FirstOrDefault(item => item.Id == "youtube:LM");
                    if (liked == null)
                    {
                        liked = new Playlist { Id = "youtube:LM", Name = "Liked Music", SourceUrl = "https://music.youtube.com/playlist?list=LM", IsRemote = true, CreatedUtc = DateTime.UtcNow };
                        library.Playlists.Add(liked);
                    }
                    liked.Tracks = new List<Track>(tracks.Select(item => item.Clone()));
                    liked.TrackCount = liked.Tracks.Count;
                    liked.TracksLoaded = true;
                    liked.ThumbnailUrl = liked.Tracks.Count == 0 ? liked.ThumbnailUrl : liked.Tracks[0].ThumbnailUrl;
                    liked.LastSyncedUtc = DateTime.UtcNow;
                }
                clientSettings.LastAccountSyncUtc = DateTime.UtcNow;
                if (!automation)
                {
                    store.Save(library);
                    settingsStore.Save(clientSettings);
                }
                RefreshPlaylistNavigation();
                if (currentPage == AppPage.Playlists) RenderPage();
                int likedCount = tracks == null ? 0 : tracks.Count;
                statusLabel.Text = "Synced " + playlistCount + " playlists and " + likedCount + " liked songs";
                youtubeAccessBody.Text = "Connected and synced automatically. Playlists refresh in the background and songs load when opened.";
                if (!quiet)
                {
                    Navigate(AppPage.Playlists, null, true);
                    string warning = playlistError == null && likedError == null ? "" : "\n\nOne part of the library could not be refreshed and the cached copy was kept.";
                    MessageBox.Show(this,
                        "Connected successfully. " + playlistCount + " YouTube playlists and " + likedCount + " liked songs are available" + (added > 0 ? "; " + added + " new liked songs were saved to Your Library." : ".") + warning,
                        afterSignIn ? "Sign-in complete" : "Account sync complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception error)
            {
                statusLabel.Text = "Account sync failed: " + error.Message;
                youtubeAccessBody.Text = error.Message;
                if (!quiet) MessageBox.Show(this, error.Message, "Account sync failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { accountSyncing = false; }
        }

        private void ImportCookies()
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Netscape cookies file|*.txt|All files|*.*", Title = "Choose exported YouTube cookies" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                clientSettings.CookieSource = "file";
                clientSettings.CookieFile = dialog.FileName;
                clientSettings.CookieProfile = "";
                clientSettings.AccessVerified = false;
                SaveAccessSettings();
            }
        }

        private void ClearAccess()
        {
            clientSettings.CookieSource = "none";
            clientSettings.CookieFile = "";
            clientSettings.CookieProfile = "";
            clientSettings.AccessVerified = false;
            clientSettings.LastAccountSyncUtc = DateTime.MinValue;
            SaveAccessSettings();
        }

        private void SaveAccessSettings()
        {
            if (!automation) settingsStore.Save(clientSettings);
            if (youtubeAccessTitle != null) youtubeAccessTitle.Text = YtDlpOptions.FriendlyName(clientSettings);
            if (youtubeAccessBody != null) youtubeAccessBody.Text = clientSettings.CookieSource == "none" ? "Choose a browser to open YouTube Music sign-in, or import cookies.txt." : (clientSettings.AccessVerified ? "The authenticated session has been verified and is ready for playback." : "This browser session has not been verified yet.");
            statusLabel.Text = clientSettings.CookieSource == "none" ? "YouTube access cleared" : YtDlpOptions.FriendlyName(clientSettings);
        }

        private void MainKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.L || e.KeyCode == Keys.K)) { searchBox.Focus(); searchBox.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.M) { ShowMiniPlayer(); e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Left) { MoveHistory(-1); e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Right) { MoveHistory(1); e.SuppressKeyPress = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.S) { ToggleShuffle(); e.SuppressKeyPress = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.R) { CycleRepeatMode(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Left) { e.SuppressKeyPress = true; BeginInvoke((Action)(async delegate { await MoveQueueAsync(-1); })); }
            else if (e.Control && e.KeyCode == Keys.Right) { e.SuppressKeyPress = true; BeginInvoke((Action)(async delegate { await MoveQueueAsync(1); })); }
            else if (e.KeyCode == Keys.Space && !(ActiveControl is TextBox)) { e.SuppressKeyPress = true; BeginInvoke((Action)(async delegate { await TogglePlaybackAsync(); })); }
        }

        protected override void WndProc(ref Message message)
        {
            const int MediaCommand = 0x0319;
            if (message.Msg == MediaCommand)
            {
                int command = (message.LParam.ToInt32() >> 16) & 0xfff;
                if (command == 14) BeginInvoke((Action)(async delegate { await TogglePlaybackAsync(); }));
                else if (command == 11) BeginInvoke((Action)(async delegate { await MoveQueueAsync(1); }));
                else if (command == 12) BeginInvoke((Action)(async delegate { await MoveQueueAsync(-1); }));
                else if (command == 13) BeginInvoke((Action)(delegate { StopPlayback(); }));
                else if (command == 8) BeginInvoke((Action)(async delegate { await ToggleMuteAsync(); }));
                else if (command == 9) BeginInvoke((Action)(async delegate { await AdjustVolumeAsync(-5); }));
                else if (command == 10) BeginInvoke((Action)(async delegate { await AdjustVolumeAsync(5); }));
            }
            base.WndProc(ref message);
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            closing = true;
            catalog.Dispose();
            playback.Dispose();
            if (miniPlayer != null && !miniPlayer.IsDisposed) miniPlayer.Close();
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
        }

        private string Greeting()
        {
            int hour = DateTime.Now.Hour;
            return hour < 12 ? "Good morning" : (hour < 18 ? "Good afternoon" : "Good evening");
        }

        private static string FormatTime(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds)) return "0:00";
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1 ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds) : string.Format("{0}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }

    }
}
