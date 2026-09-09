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
            foreach (Control control in playlistNavigation.Controls)
            {
                NavButton button = control as NavButton;
                if (button != null) { button.Selected = page == AppPage.Playlist && string.Equals(Convert.ToString(button.Tag), playlist == null ? null : playlist.Id, StringComparison.Ordinal); button.Invalidate(); }
            }
            backButton.Enabled = historyIndex > 0;
            forwardButton.Enabled = historyIndex >= 0 && historyIndex < history.Count - 1;
            RenderPage();
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
            settingsPanel.Visible = currentPage == AppPage.Settings;
            trackList.Visible = currentPage != AppPage.Settings;
            homeRow.Height = currentPage == AppPage.Home ? 96 : 0;

            List<Track> visible = VisibleTracks();
            switch (currentPage)
            {
                case AppPage.Home:
                    heading.Text = Greeting();
                    subtitle.Text = library.RecentTracks.Count > 0 ? "Pick up where you left off" : "Your music, without a browser running in the background";
                    AddAction("Play", AppIcon.Play, true, async delegate { await PlayFirstAsync(); });
                    AddAction("Import audio", AppIcon.Add, false, ImportAudio);
                    PopulateHomeTiles(visible);
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
                case AppPage.Playlist:
                    heading.Text = currentPlaylist == null ? "Playlist" : currentPlaylist.Name;
                    subtitle.Text = visible.Count + (visible.Count == 1 ? " song" : " songs");
                    AddTrackActions(true);
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
            trackList.SetTracks(visible);
            UpdateActionState();
            if (currentPage == AppPage.Search && !searching) searchBox.Focus();
        }

        private List<Track> VisibleTracks()
        {
            if (currentPage == AppPage.Search) return searchResults;
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
            searchButton.Enabled = false;
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
                searchButton.Enabled = true;
                if (!closing && currentPage == AppPage.Search) RenderPage();
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
            int next = queueIndex + offset;
            if (next < 0 || next >= queue.Count) { statusLabel.Text = offset > 0 ? "End of queue" : "Start of queue"; return; }
            queueIndex = next;
            await StartQueueTrackAsync(queue[queueIndex]);
            if (currentPage == AppPage.Queue) RenderPage();
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

        private void RefreshPlaylistNavigation()
        {
            playlistNavigation.Controls.Clear();
            foreach (Playlist playlist in library.Playlists.OrderBy(item => item.Name))
            {
                Playlist target = playlist;
                NavButton button = new NavButton { Label = target.Name, Icon = AppIcon.Playlist, AccessibleName = "Playlist " + target.Name, Width = playlistNavigation.ClientSize.Width - 2, Tag = target.Id };
                button.Click += delegate { Navigate(AppPage.Playlist, target, true); };
                playlistNavigation.Controls.Add(button);
            }
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
            using (Form dialog = new Form { Text = title, Size = new Size(420, 175), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, BackColor = Theme.Window, ForeColor = Theme.Text, Font = Font })
            {
                TextBox field = new TextBox { Left = 20, Top = 24, Width = 364, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Text = placeholder };
                PillButton confirm = new PillButton { Left = 266, Top = 70, Width = 118, Label = "Create", Primary = true };
                Button accept = new Button { Visible = false, DialogResult = DialogResult.OK };
                confirm.Click += delegate { dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                dialog.Controls.Add(field); dialog.Controls.Add(confirm); dialog.Controls.Add(accept); dialog.AcceptButton = accept; field.SelectAll();
                return dialog.ShowDialog(this) == DialogResult.OK ? field.Text.Trim() : null;
            }
        }

        private void UpdateActionState()
        {
            Track selected = trackList.SelectedTrack;
            foreach (Control control in actionBar.Controls) control.Enabled = selected != null || Convert.ToString(control.Tag) == "Import audio" || Convert.ToString(control.Tag) == "Clear queue";
        }

        private void PlaybackSnapshotChanged(object sender, PlaybackSnapshot update)
        {
            if (closing) return;
            try { BeginInvoke((Action)(delegate { ApplySnapshot(update); })); } catch { }
        }

        private void ApplySnapshot(PlaybackSnapshot update)
        {
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
            progressSlider.Value = Math.Min(progressSlider.Maximum, update.PositionSeconds);
            elapsedLabel.Text = FormatTime(update.PositionSeconds);
            durationLabel.Text = FormatTime(duration);
            updatingProgress = false;
            if (miniPlayer != null) miniPlayer.ApplySnapshot(update);
        }

        private void PlaybackEnded(object sender, EventArgs eventArgs)
        {
            if (closing) return;
            try { BeginInvoke((Action)(async delegate { await MoveQueueAsync(1); })); } catch { }
        }

        private void ShowMiniPlayer()
        {
            if (miniPlayer != null && !miniPlayer.IsDisposed) { miniPlayer.Activate(); return; }
            miniPlayer = new MiniPlayerWindow(playback, async delegate { await MoveQueueAsync(-1); }, async delegate { await MoveQueueAsync(1); }, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            miniPlayer.FormClosed += delegate { miniPlayer = null; };
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

        private void MainKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.L || e.KeyCode == Keys.K)) { searchBox.Focus(); searchBox.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.M) { ShowMiniPlayer(); e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Left) { MoveHistory(-1); e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Right) { MoveHistory(1); e.SuppressKeyPress = true; }
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
