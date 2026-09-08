using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal sealed partial class NativePlayer
{
    public sealed class Track { public string Title { get; set; } public string Artist { get; set; } public string Source { get; set; } }
    public sealed class Collection { public List<Track> Tracks = new List<Track>(); public Dictionary<string, List<Track>> Playlists = new Dictionary<string, List<Track>>(); }
    readonly Color background = Color.FromArgb(19, 20, 25), surface = Color.FromArgb(29, 31, 39), accent = Color.FromArgb(246, 88, 111);
    readonly List<Track> queue = new List<Track>();
    Collection library = new Collection();
    List<Track> results = new List<Track>();
    ListView tracks;
    Label heading, subtitle, nowPlaying;
    TextBox search;
    FlowLayoutPanel navigation, actions;
    string page = "Home", playlistName;
    int queueIndex = -1;
    Process searchProcess;
    Form mini;
    readonly string libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicLiteNative", "library.json");
    Button UiButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 36, MinimumSize = new Size(80, 36), FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = Color.White, Margin = new Padding(4), AccessibleName = text };
        button.FlatAppearance.BorderSize = 0;
        button.Click += delegate { action(); };
        return button;
    }
    void BuildMusicUi()
    {
        Text = "YT Music Lite"; Size = new Size(1060, 720); MinimumSize = new Size(860, 600);
        Font = new Font("Segoe UI", 10); BackColor = background; ForeColor = Color.White; StartPosition = FormStartPosition.CenterScreen;
        try { if (!benchmark && File.Exists(libraryPath)) library = new JavaScriptSerializer().Deserialize<Collection>(File.ReadAllText(libraryPath)) ?? new Collection(); }
        catch (Exception) { status.Text = "Your saved library could not be read. The original file is preserved."; }
        library.Tracks = library.Tracks ?? new List<Track>(); library.Playlists = library.Playlists ?? new Dictionary<string, List<Track>>();
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(14, 24, 10, 10), BackColor = surface };
        root.Controls.Add(navigation, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 5, ColumnCount = 1 };
        foreach (int height in new[] { 46, 42, 36, 48 }) body.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        search = new TextBox { Dock = DockStyle.Fill, BackColor = surface, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Search songs or artists", Margin = new Padding(0, 6, 8, 0) };
        search.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
        searchRow.Controls.Add(search, 0, 0); searchRow.Controls.Add(UiButton("Search", async delegate { await SearchAsync(); }), 1, 0);
        heading = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 22, FontStyle.Bold) };
        subtitle = new Label { Dock = DockStyle.Fill, ForeColor = Color.Silver, AutoEllipsis = true };
        actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        tracks = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, BackColor = background, ForeColor = Color.White, BorderStyle = BorderStyle.None, AccessibleName = "Tracks" };
        tracks.Columns.Add("Title", 330); tracks.Columns.Add("Artist / source", 230);
        tracks.DoubleClick += async delegate { await PlaySelected(); };
        tracks.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await PlaySelected(); } };
        body.Controls.Add(searchRow, 0, 0); body.Controls.Add(heading, 0, 1); body.Controls.Add(subtitle, 0, 2); body.Controls.Add(actions, 0, 3); body.Controls.Add(tracks, 0, 4); root.Controls.Add(body, 1, 0);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 8, 18, 8), RowCount = 3, ColumnCount = 1, BackColor = surface };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nowPlaying = new Label { Dock = DockStyle.Fill, Text = "Choose something to play", AutoEllipsis = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
        var transport = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        foreach (var button in new[] { play, pause, stop }) { button.FlatStyle = FlatStyle.Flat; button.BackColor = background; button.ForeColor = Color.White; button.Height = 36; button.MinimumSize = new Size(80, 36); button.Margin = new Padding(4); }
        play.Text = "Play"; pause.Text = "Pause / resume";
        transport.Controls.Add(UiButton("Previous", async delegate { await MoveQueue(-1); })); transport.Controls.AddRange(new Control[] { play, pause, stop });
        transport.Controls.Add(UiButton("Next", async delegate { await MoveQueue(1); })); transport.Controls.Add(UiButton("Mini player", ShowMini));
        var volume = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Width = 115, TickStyle = TickStyle.None, AccessibleName = "Volume" };
        volume.MouseUp += async delegate { try { await CommandAsync("set volume " + volume.Value); } catch (Exception e) { status.Text = e.Message; } };
        volume.KeyUp += async delegate { try { await CommandAsync("set volume " + volume.Value); } catch (Exception e) { status.Text = e.Message; } };
        transport.Controls.Add(volume); status.ForeColor = Color.Silver; status.Text = "Search music or add your audio files to get started.";
        footer.Controls.Add(nowPlaying, 0, 0); footer.Controls.Add(transport, 0, 1); footer.Controls.Add(status, 0, 2); root.Controls.Add(footer, 0, 1); root.SetColumnSpan(footer, 2); Controls.Add(root);
        RefreshNavigation(); ShowPage("Home", null);
    }
    void RefreshNavigation()
    {
        foreach (Control c in navigation.Controls.Cast<Control>().ToArray()) c.Dispose();
        navigation.Controls.Add(new Label { Text = "YT MUSIC\nLITE", Height = 76, Width = 160, ForeColor = accent, Font = new Font("Segoe UI", 18, FontStyle.Bold) });
        foreach (string name in new[] { "Home", "Search", "Library", "Queue", "Settings" }) { string target = name; var b = UiButton(target, delegate { ShowPage(target, null); }); b.Width = 155; navigation.Controls.Add(b); }
        navigation.Controls.Add(UiButton("+ New playlist", CreatePlaylist));
        foreach (string name in library.Playlists.Keys.OrderBy(x => x)) { string target = name; var b = UiButton(target, delegate { ShowPage("Playlist", target); }); b.Width = 155; navigation.Controls.Add(b); }
    }
    List<Track> VisibleTracks()
    {
        if (page == "Settings") return new List<Track>();
        if (page == "Search") return results;
        if (page == "Queue") return queue;
        if (page == "Playlist") return library.Playlists[playlistName];
        return library.Tracks;
    }
    void ShowPage(string name, string selectedPlaylist)
    {
        page = name; playlistName = selectedPlaylist; heading.Text = name == "Playlist" ? selectedPlaylist : name;
        foreach (Control c in actions.Controls.Cast<Control>().ToArray()) c.Dispose();
        if (name == "Settings")
        {
            actions.Controls.Add(UiButton("Check for updates", async delegate { await CheckUpdatesAsync(); }));
            RenderTracks();
            subtitle.Text = "YT Music Lite " + YTMusicLiteNative.UpdateService.CurrentVersion + " · Browser-free Windows audio player";
            return;
        }
        actions.Controls.Add(UiButton("Play selected", async delegate { await PlaySelected(); }));
        actions.Controls.Add(UiButton("Add to queue", delegate { var t = Selected(); if (t != null) { queue.Add(t); status.Text = "Added to queue"; if (page == "Queue") RenderTracks(); } }));
        actions.Controls.Add(UiButton("Save to library", delegate { var t = Selected(); if (t != null) { if (!library.Tracks.Any(x => x.Source == t.Source)) library.Tracks.Add(t); SaveLibrary(); } }));
        actions.Controls.Add(UiButton("Add to playlist", AddToPlaylist));
        if (name == "Home" || name == "Library") actions.Controls.Add(UiButton("Import audio", ImportAudio));
        if (name == "Queue" || name == "Playlist" || name == "Library") actions.Controls.Add(UiButton("Remove", RemoveSelected));
        RenderTracks(); if (name == "Search") search.Focus();
    }
    void RenderTracks()
    {
        var items = VisibleTracks(); tracks.BeginUpdate(); tracks.Items.Clear();
        foreach (var t in items) tracks.Items.Add(new ListViewItem(new[] { t.Title ?? "Untitled", t.Artist ?? "" }) { Tag = t });
        tracks.EndUpdate(); subtitle.Text = items.Count == 0 ? (page == "Search" ? "Search for a song or artist above." : "No tracks yet. Search music or import audio from Home.") : items.Count + " tracks · Double-click or press Enter to play";
    }
    Track Selected() { if (tracks.SelectedItems.Count == 0) { status.Text = "Select a track first."; return null; } return (Track)tracks.SelectedItems[0].Tag; }
    async Task PlaySelected()
    {
        var t = Selected(); if (t == null || busy) return;
        int index = tracks.SelectedIndices[0];
        if (page != "Queue") { var copy = VisibleTracks().ToList(); queue.Clear(); queue.AddRange(copy); }
        queueIndex = index; await PlayTrack(t);
    }
    async Task PlayTrack(Track t) { input.Text = t.Source; nowPlaying.Text = t.Title + "  ·  " + t.Artist; await PlayAsync(); }
    async Task MoveQueue(int offset) { if (busy || queue.Count == 0) return; int next = queueIndex + offset; if (next < 0 || next >= queue.Count) return; queueIndex = next; await PlayTrack(queue[next]); }
    async void WatchPlayer(Process process, int request)
    {
        try { await Task.Run(delegate { process.WaitForExit(); }); if (closing || request != generation) return; int code = process.ExitCode; if (code != 0) { status.Text = "Playback failed. Select the track and try again."; return; } status.Text = "Finished"; await MoveQueue(1); }
        catch (InvalidOperationException) { }
    }
    void ImportAudio()
    {
        using (var dialog = new OpenFileDialog { Multiselect = true, Filter = "Audio|*.mp3;*.m4a;*.ogg;*.opus;*.wav;*.flac" })
            if (dialog.ShowDialog(this) == DialogResult.OK) { foreach (string file in dialog.FileNames) if (!library.Tracks.Any(x => x.Source == file)) library.Tracks.Add(new Track { Title = Path.GetFileNameWithoutExtension(file), Artist = "Local audio", Source = file }); SaveLibrary(); RenderTracks(); }
    }
    string Prompt(string title, string initial)
    {
        using (var dialog = new Form { Text = title, Size = new Size(380, 170), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false }) {
            var field = new TextBox { Left = 16, Top = 18, Width = 330, Text = initial }; var ok = new Button { Text = "Save", Left = 260, Top = 60, DialogResult = DialogResult.OK }; dialog.Controls.AddRange(new Control[] { field, ok }); dialog.AcceptButton = ok;
            return dialog.ShowDialog(this) == DialogResult.OK ? field.Text.Trim() : null;
        }
    }
    void CreatePlaylist() { string name = Prompt("New playlist name", ""); if (string.IsNullOrWhiteSpace(name)) return; if (library.Playlists.ContainsKey(name)) { status.Text = "That playlist already exists."; return; } library.Playlists.Add(name, new List<Track>()); SaveLibrary(); RefreshNavigation(); ShowPage("Playlist", name); }
    void AddToPlaylist() { var t = Selected(); if (t == null) return; var menu = new ContextMenuStrip(); foreach (string name in library.Playlists.Keys.OrderBy(x => x)) { string key = name; menu.Items.Add(key, null, delegate { if (!library.Playlists[key].Any(x => x.Source == t.Source)) library.Playlists[key].Add(t); SaveLibrary(); if (page == "Playlist") RenderTracks(); }); } if (menu.Items.Count == 0) { status.Text = "Create a playlist in the sidebar first."; menu.Dispose(); return; } menu.Closed += delegate { menu.Dispose(); }; menu.Show(Cursor.Position); }
    void RemoveSelected() { if (Selected() == null) return; int index = tracks.SelectedIndices[0]; VisibleTracks().RemoveAt(index); if (page == "Queue" && index <= queueIndex) queueIndex--; if (page != "Queue") SaveLibrary(); RenderTracks(); }
    void SaveLibrary()
    {
        if (benchmark) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)); string temp = libraryPath + ".tmp"; File.WriteAllText(temp, new JavaScriptSerializer().Serialize(library)); if (File.Exists(libraryPath)) File.Replace(temp, libraryPath, libraryPath + ".bak"); else File.Move(temp, libraryPath); status.Text = "Saved"; }
        catch (Exception e) { status.Text = "Could not save library: " + e.Message; }
    }
    async Task SearchAsync()
    {
        if (searchProcess != null || string.IsNullOrWhiteSpace(search.Text)) return;
        Process process = null;
        try {
            string query = search.Text.Trim(); ShowPage("Search", null); subtitle.Text = "Searching…";
            string deno = FindTool("deno");
            process = Start(FindTool("yt-dlp"), "--ignore-config --js-runtimes " + Quote("deno:" + deno) + " --flat-playlist --dump-json --no-warnings --socket-timeout 15 --retries 1 -- " + Quote("ytsearch20:" + query), true); searchProcess = process;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); var exited = Task.Run(delegate { process.WaitForExit(); });
            if (await Task.WhenAny(exited, Task.Delay(45000)) != exited) { KillTree(process); throw new TimeoutException("Search timed out. Try again."); }
            await exited; string json = await output; string errors = await error; if (closing) return;
            if (process.ExitCode != 0) throw new InvalidOperationException("Search failed. Check your connection and yt-dlp installation. " + errors.Substring(0, Math.Min(160, errors.Length)));
            var found = new List<Track>(); var serializer = new JavaScriptSerializer();
            foreach (string line in json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                var data = serializer.Deserialize<Dictionary<string, object>>(line); object id, title, artist;
                if (!data.TryGetValue("id", out id) || id == null) continue;
                data.TryGetValue("title", out title); if (!data.TryGetValue("channel", out artist)) data.TryGetValue("uploader", out artist);
                found.Add(new Track { Title = Convert.ToString(title), Artist = Convert.ToString(artist), Source = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(Convert.ToString(id)) });
            }
            results = found; if (page == "Search") { RenderTracks(); if (found.Count == 0) subtitle.Text = "No results. Try another song or artist."; } status.Text = "Search complete";
        } catch (Exception e) { if (!closing) { status.Text = e.Message; if (page == "Search") subtitle.Text = "Search unavailable. You can retry or play imported audio."; } }
        finally { if (process != null) { KillTree(process); process.Dispose(); } searchProcess = null; }
    }
    void RunUiCheck()
    {
        try {
            var track = new Track { Title = "A long song title to check readable track rows", Artist = "Sample artist", Source = "sample.wav" };
            library.Tracks.Add(track); library.Playlists.Add("Favorites", new List<Track> { track }); RefreshNavigation();
            ShowPage("Library", null); if (tracks.Items.Count != 1) throw new Exception("Library navigation failed");
            ShowPage("Playlist", "Favorites"); if (tracks.Items.Count != 1 || heading.Text != "Favorites") throw new Exception("Playlist navigation failed");
            queue.Add(track); queue.Add(track); queueIndex = 1; ShowPage("Queue", null); tracks.Items[0].Selected = true; RemoveSelected();
            if (queue.Count != 1 || queueIndex != 0) throw new Exception("Queue removal did not preserve active position");
            ShowPage("Search", null); if (tracks.Items.Count != 0) throw new Exception("Search state leaked library tracks");
            ShowPage("Settings", null); if (actions.Controls.Count != 1 || !subtitle.Text.Contains(YTMusicLiteNative.UpdateService.CurrentVersion)) throw new Exception("Settings/update page failed");
            ShowPage("Home", null); ShowMini(); if (mini == null) throw new Exception("Mini player failed"); mini.Close();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save("native-home.png"); }
            Size = MinimumSize;
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save("native-compact.png"); }
            File.WriteAllText("ui-check.txt", "PASS: navigation, playlist display, queue removal, search isolation, settings updater, mini player");
        } catch (Exception e) { File.WriteAllText("ui-check.txt", "FAIL: " + e); Environment.ExitCode = 1; }
        Close();
    }
    void ShowMini()
    {
        if (mini != null) { mini.Activate(); return; }
        mini = new Form { Text = "Now playing", Size = new Size(440, 175), MinimumSize = new Size(440, 175), BackColor = surface, ForeColor = Color.White, TopMost = true, Font = Font, StartPosition = FormStartPosition.CenterParent };
        var title = new Label { Dock = DockStyle.Top, Height = 40, Text = nowPlaying.Text, AutoEllipsis = true, Padding = new Padding(10) }; EventHandler update = delegate { title.Text = nowPlaying.Text; }; nowPlaying.TextChanged += update;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8) }; buttons.Controls.Add(UiButton("Pause / resume", delegate { pause.PerformClick(); })); buttons.Controls.Add(UiButton("Next", async delegate { await MoveQueue(1); })); buttons.Controls.Add(UiButton("Stop", delegate { stop.PerformClick(); }));
        mini.Controls.Add(buttons); mini.Controls.Add(title); mini.FormClosed += delegate { nowPlaying.TextChanged -= update; mini = null; }; mini.Show(this);
    }
    async Task CheckUpdatesAsync()
    {
        try
        {
            status.Text = "Checking for updates…";
            var service = new YTMusicLiteNative.UpdateService();
            var update = await service.CheckAsync();
            if (!update.UpdateAvailable) { status.Text = update.Message; return; }
            if (MessageBox.Show(this, update.Message + "\n\nDownload and install it now?", "YT Music Lite update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) { status.Text = "Update available"; return; }
            status.Text = "Downloading update…";
            var progress = new Progress<int>(delegate(int percent) { status.Text = "Downloading update… " + percent + "%"; });
            var prepared = await service.PrepareAsync(update, progress);
            status.Text = "Restarting to install " + update.Version + "…";
            service.InstallPrepared(prepared);
            Close();
        }
        catch (Exception e) { status.Text = "Update failed: " + e.Message; }
    }
}
