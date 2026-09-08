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
    const string SearchHint = "What do you want to play?";
    public sealed class Track { public string Title { get; set; } public string Artist { get; set; } public string Source { get; set; } }
    public sealed class Collection { public List<Track> Tracks = new List<Track>(); public Dictionary<string, List<Track>> Playlists = new Dictionary<string, List<Track>>(); }
    readonly Color background = MusicTheme.Canvas, surface = MusicTheme.Raised, accent = MusicTheme.Accent;
    readonly List<Track> queue = new List<Track>();
    readonly Dictionary<string, NavButton> navButtons = new Dictionary<string, NavButton>();
    Collection library = new Collection();
    List<Track> results = new List<Track>();
    MusicListView tracks;
    Label heading, subtitle, nowPlaying, nowArtist;
    TextBox search;
    FlowLayoutPanel navigation, actions, quickCards;
    RowStyle quickRow;
    string page = "Home", playlistName;
    int queueIndex = -1;
    Process searchProcess;
    Form mini;
    bool searchHintVisible;
    readonly string libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicLiteNative", "library.json");

    Button UiButton(string text, Action action)
    {
        var button = new MusicButton { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(86, 34), BackColor = MusicTheme.Raised, ForeColor = MusicTheme.Text, Margin = new Padding(0, 2, 8, 2), AccessibleName = text };
        button.Click += delegate { action(); }; return button;
    }

    MusicButton IconButton(string text, Action action, bool primary)
    {
        var button = new MusicButton { Text = text, Width = primary ? 48 : 40, Height = primary ? 48 : 40, Radius = primary ? 24 : 20, Emphasized = primary, BackColor = Color.Transparent, ForeColor = MusicTheme.Text, Margin = new Padding(5, primary ? 0 : 4, 5, 0), AccessibleName = text };
        button.Font = new Font("Segoe UI Symbol", primary ? 15 : 11, FontStyle.Bold); button.Click += delegate { action(); }; return button;
    }

    void BuildMusicUi()
    {
        Text = "YT Music Lite"; Size = new Size(1180, 760); MinimumSize = new Size(900, 620); Font = new Font("Segoe UI", 9); BackColor = MusicTheme.Window; ForeColor = MusicTheme.Text; StartPosition = FormStartPosition.CenterScreen;
        try { if (!benchmark && File.Exists(libraryPath)) library = new JavaScriptSerializer().Deserialize<Collection>(File.ReadAllText(libraryPath)) ?? new Collection(); }
        catch (Exception) { status.Text = "Your saved library could not be read. The original file is preserved."; }
        library.Tracks = library.Tracks ?? new List<Track>(); library.Playlists = library.Playlists ?? new Dictionary<string, List<Track>>();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = MusicTheme.Window, Margin = Padding.Empty, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = MusicTheme.Sidebar, Margin = new Padding(0, 0, 8, 8), Padding = new Padding(12) };
        var brand = new TableLayoutPanel { Dock = DockStyle.Top, Height = 66, ColumnCount = 2, BackColor = MusicTheme.Sidebar };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.Controls.Add(new ArtworkBox { Dock = DockStyle.Fill, Margin = new Padding(2, 8, 5, 12), KeyText = "YT Music Lite" }, 0, 0);
        brand.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "YT MUSIC LITE", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = MusicTheme.Text, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
        navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = MusicTheme.Sidebar, Padding = new Padding(0, 6, 0, 0) };
        sidebar.Controls.Add(navigation); sidebar.Controls.Add(brand); root.Controls.Add(sidebar, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(26, 10, 26, 12), BackColor = MusicTheme.Canvas, RowCount = 6, ColumnCount = 1 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); quickRow = new RowStyle(SizeType.Absolute, 88); body.RowStyles.Add(quickRow); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = MusicTheme.Canvas };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        var searchShell = new RoundPanel { Dock = DockStyle.Left, Width = 430, Height = 40, Radius = 20, Margin = new Padding(0, 8, 0, 8), BackColor = Color.White, Padding = new Padding(17, 10, 12, 7) };
        search = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Color.FromArgb(90, 90, 90), Font = new Font("Segoe UI", 10), AccessibleName = "Search songs or artists", Text = SearchHint }; searchHintVisible = true;
        search.GotFocus += delegate { if (searchHintVisible) { search.Text = ""; search.ForeColor = Color.FromArgb(25, 25, 25); searchHintVisible = false; } };
        search.LostFocus += delegate { if (string.IsNullOrWhiteSpace(search.Text)) { search.Text = SearchHint; search.ForeColor = Color.FromArgb(90, 90, 90); searchHintVisible = true; } };
        search.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } }; searchShell.Controls.Add(search);
        var searchButton = new MusicButton { Text = "Search", Dock = DockStyle.Fill, Margin = new Padding(8), BackColor = MusicTheme.Raised, AccessibleName = "Search" }; searchButton.Click += async delegate { await SearchAsync(); };
        top.Controls.Add(searchShell, 0, 0); top.Controls.Add(searchButton, 1, 0);

        heading = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = MusicTheme.Text, TextAlign = ContentAlignment.BottomLeft };
        subtitle = new Label { Dock = DockStyle.Fill, ForeColor = MusicTheme.Muted, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        quickCards = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = MusicTheme.Canvas, Padding = new Padding(0, 8, 0, 0) };
        actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = MusicTheme.Canvas, Padding = new Padding(0, 5, 0, 0) };
        tracks = new MusicListView { Dock = DockStyle.Fill, AccessibleName = "Tracks" };
        tracks.Columns.Add("#", 46); tracks.Columns.Add("Title", 370); tracks.Columns.Add("Artist", 235); tracks.Columns.Add("Source", 175);
        tracks.Resize += delegate { ResizeTrackColumns(); };
        tracks.DoubleClick += async delegate { await PlaySelected(); }; tracks.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await PlaySelected(); } };
        body.Controls.Add(top, 0, 0); body.Controls.Add(heading, 0, 1); body.Controls.Add(subtitle, 0, 2); body.Controls.Add(quickCards, 0, 3); body.Controls.Add(actions, 0, 4); body.Controls.Add(tracks, 0, 5); root.Controls.Add(body, 1, 0);

        root.Controls.Add(BuildPlayerBar(), 0, 1); root.SetColumnSpan(root.GetControlFromPosition(0, 1), 2); Controls.Add(root); RefreshNavigation(); ShowPage("Home", null);
    }

    void ResizeTrackColumns()
    {
        if (tracks == null || tracks.Columns.Count < 4) return; int available = Math.Max(476, tracks.ClientSize.Width - 30);
        tracks.Columns[0].Width = 42; tracks.Columns[1].Width = Math.Max(200, (available - 42) * 45 / 100); tracks.Columns[2].Width = Math.Max(130, (available - 42) * 31 / 100); tracks.Columns[3].Width = Math.Max(100, available - tracks.Columns[0].Width - tracks.Columns[1].Width - tracks.Columns[2].Width);
    }

    Control BuildPlayerBar()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = MusicTheme.Player, Margin = Padding.Empty, Padding = new Padding(16, 10, 16, 8) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = MusicTheme.Player };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); info.RowStyles.Add(new RowStyle(SizeType.Absolute, 31)); info.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var art = new ArtworkBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), KeyText = "YT Music Lite" };
        nowPlaying = new Label { Dock = DockStyle.Fill, Text = "Nothing playing", AutoEllipsis = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = MusicTheme.Text, TextAlign = ContentAlignment.BottomLeft };
        nowArtist = new Label { Dock = DockStyle.Fill, Text = "Choose a song", AutoEllipsis = true, ForeColor = MusicTheme.Muted, TextAlign = ContentAlignment.TopLeft };
        status.ForeColor = MusicTheme.Muted; status.Text = "Ready"; status.Dock = DockStyle.Fill; status.AutoEllipsis = true; status.Font = new Font("Segoe UI", 8);
        info.Controls.Add(art, 0, 0); info.SetRowSpan(art, 3); info.Controls.Add(nowPlaying, 1, 0); info.Controls.Add(nowArtist, 1, 1); info.Controls.Add(status, 1, 2);

        var transport = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = MusicTheme.Player, Padding = new Padding(0, 10, 0, 0) };
        transport.Controls.Add(IconButton("◀|", async delegate { await MoveQueue(-1); }, false)); transport.Controls.Add(IconButton("▶", delegate { if (player == null || player.HasExited) play.PerformClick(); else pause.PerformClick(); }, true)); transport.Controls.Add(IconButton("■", delegate { stop.PerformClick(); }, false)); transport.Controls.Add(IconButton("|▶", async delegate { await MoveQueue(1); }, false));

        var utility = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = MusicTheme.Player, Padding = new Padding(0, 18, 0, 0) };
        var volume = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, Width = 112, TickStyle = TickStyle.None, AccessibleName = "Volume", Margin = new Padding(4, 4, 0, 0) };
        volume.MouseUp += async delegate { try { await CommandAsync("set volume " + volume.Value); } catch (Exception e) { status.Text = e.Message; } }; volume.KeyUp += async delegate { try { await CommandAsync("set volume " + volume.Value); } catch (Exception e) { status.Text = e.Message; } };
        utility.Controls.Add(volume); utility.Controls.Add(UiButton("Mini player", ShowMini)); footer.Controls.Add(info, 0, 0); footer.Controls.Add(transport, 1, 0); footer.Controls.Add(utility, 2, 0); return footer;
    }

    void RefreshNavigation()
    {
        foreach (Control c in navigation.Controls.Cast<Control>().ToArray()) c.Dispose(); navButtons.Clear(); AddNav("Home"); AddNav("Search"); AddNav("Library"); AddNav("Queue"); AddNav("Settings");
        navigation.Controls.Add(new Label { Text = "YOUR PLAYLISTS", Width = 168, Height = 38, Margin = Padding.Empty, Padding = new Padding(12, 14, 0, 0), ForeColor = MusicTheme.Muted, Font = new Font("Segoe UI", 8, FontStyle.Bold) });
        var create = new NavButton { Text = "+  Create playlist", Font = new Font("Segoe UI", 9, FontStyle.Bold), AccessibleName = "Create playlist" }; create.Click += delegate { CreatePlaylist(); }; navigation.Controls.Add(create);
        foreach (string name in library.Playlists.Keys.OrderBy(x => x)) { string target = name; var button = new NavButton { Text = target, Font = new Font("Segoe UI", 9), AccessibleName = "Playlist " + target }; button.Click += delegate { ShowPage("Playlist", target); }; navigation.Controls.Add(button); navButtons["Playlist:" + target] = button; }
    }

    void AddNav(string name) { string target = name; var button = new NavButton { Text = name, AccessibleName = name }; button.Click += delegate { ShowPage(target, null); }; navigation.Controls.Add(button); navButtons[name] = button; }
    List<Track> VisibleTracks() { if (page == "Settings") return new List<Track>(); if (page == "Search") return results; if (page == "Queue") return queue; if (page == "Playlist") return library.Playlists[playlistName]; return library.Tracks; }

    void ShowPage(string name, string selectedPlaylist)
    {
        page = name; playlistName = selectedPlaylist; foreach (var pair in navButtons) { pair.Value.Selected = pair.Key == (name == "Playlist" ? "Playlist:" + selectedPlaylist : name); pair.Value.Invalidate(); }
        heading.Text = name == "Home" ? Greeting() : (name == "Playlist" ? selectedPlaylist : name); foreach (Control c in actions.Controls.Cast<Control>().ToArray()) c.Dispose(); quickCards.Controls.Clear(); quickRow.Height = name == "Home" ? 88 : 0;
        if (name == "Settings") { actions.Controls.Add(UiButton("Check for updates", async delegate { await CheckUpdatesAsync(); })); RenderTracks(); subtitle.Text = "YT Music Lite " + YTMusicLiteNative.UpdateService.CurrentVersion + " · Native playback · Low memory mode"; return; }
        actions.Controls.Add(UiButton("Play", async delegate { await PlaySelected(); })); actions.Controls.Add(UiButton("Add to queue", AddSelectedToQueue)); actions.Controls.Add(UiButton("Save", SaveSelected)); actions.Controls.Add(UiButton("Add to playlist", AddToPlaylist));
        if (name == "Home" || name == "Library") actions.Controls.Add(UiButton("Import audio", ImportAudio)); if (name == "Queue" || name == "Playlist" || name == "Library") actions.Controls.Add(UiButton("Remove", RemoveSelected));
        if (name == "Home") PopulateQuickCards(); RenderTracks(); if (name == "Search") search.Focus(); else navigation.Focus();
    }

    string Greeting() { int hour = DateTime.Now.Hour; return hour < 12 ? "Good morning" : (hour < 18 ? "Good afternoon" : "Good evening"); }
    void PopulateQuickCards()
    {
        foreach (Track track in library.Tracks.Take(4)) { Track target = track; quickCards.Controls.Add(new QuickCard(target.Title, async delegate { queue.Clear(); queue.Add(target); queueIndex = 0; await PlayTrack(target); })); }
        foreach (string name in library.Playlists.Keys.Take(Math.Max(0, 4 - quickCards.Controls.Count))) { string target = name; quickCards.Controls.Add(new QuickCard(target, delegate { ShowPage("Playlist", target); })); }
        if (quickCards.Controls.Count == 0) quickCards.Controls.Add(new Label { Text = "Your saved music and playlists will appear here.", ForeColor = MusicTheme.Muted, AutoSize = true, Padding = new Padding(0, 24, 0, 0) });
    }

    void RenderTracks()
    {
        var items = VisibleTracks(); tracks.BeginUpdate(); tracks.Items.Clear(); int index = 1;
        foreach (var track in items) tracks.Items.Add(new ListViewItem(new[] { (index++).ToString(), track.Title ?? "Untitled", track.Artist ?? "Unknown artist", SourceLabel(track.Source) }) { Tag = track }); tracks.EndUpdate();
        if (items.Count == 0) subtitle.Text = page == "Search" ? "Search for songs and artists" : (page == "Queue" ? "Your queue is empty" : "Start with Search or import audio from your computer"); else subtitle.Text = items.Count + (items.Count == 1 ? " song" : " songs");
    }

    string SourceLabel(string source) { return File.Exists(source ?? "") ? "On this computer" : "YouTube"; }
    Track Selected() { if (tracks.SelectedItems.Count == 0) { status.Text = "Select a song first"; return null; } return (Track)tracks.SelectedItems[0].Tag; }
    async Task PlaySelected() { var track = Selected(); if (track == null || busy) return; int index = tracks.SelectedIndices[0]; if (page != "Queue") { queue.Clear(); queue.AddRange(VisibleTracks()); } queueIndex = index; await PlayTrack(track); }
    async Task PlayTrack(Track track) { input.Text = track.Source; nowPlaying.Text = track.Title; nowArtist.Text = track.Artist; await PlayAsync(); }
    async Task MoveQueue(int offset) { if (busy || queue.Count == 0) return; int next = queueIndex + offset; if (next < 0 || next >= queue.Count) return; queueIndex = next; await PlayTrack(queue[next]); }
    void AddSelectedToQueue() { var track = Selected(); if (track == null) return; queue.Add(track); status.Text = "Added to queue"; if (page == "Queue") RenderTracks(); }
    void SaveSelected() { var track = Selected(); if (track == null) return; if (!library.Tracks.Any(x => x.Source == track.Source)) library.Tracks.Add(track); SaveLibrary(); }

    async void WatchPlayer(Process process, int request)
    {
        try { await Task.Run(delegate { process.WaitForExit(); }); if (closing || request != generation) return; if (process.ExitCode != 0) { status.Text = "Playback failed. Select the song and try again."; return; } status.Text = "Finished"; await MoveQueue(1); }
        catch (InvalidOperationException) { }
    }

    void ImportAudio()
    {
        using (var dialog = new OpenFileDialog { Multiselect = true, Filter = "Audio|*.mp3;*.m4a;*.ogg;*.opus;*.wav;*.flac" }) if (dialog.ShowDialog(this) == DialogResult.OK) { foreach (string file in dialog.FileNames) if (!library.Tracks.Any(x => x.Source == file)) library.Tracks.Add(new Track { Title = Path.GetFileNameWithoutExtension(file), Artist = "Local audio", Source = file }); SaveLibrary(); ShowPage("Library", null); }
    }

    string Prompt(string title, string initial)
    {
        using (var dialog = new Form { Text = title, Size = new Size(390, 170), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, BackColor = MusicTheme.Canvas, ForeColor = MusicTheme.Text })
        {
            var field = new TextBox { Left = 18, Top = 20, Width = 338, Text = initial, BackColor = MusicTheme.Raised, ForeColor = MusicTheme.Text, BorderStyle = BorderStyle.FixedSingle }; var ok = new MusicButton { Text = "Create", Left = 266, Top = 64, Width = 90, Height = 34, Emphasized = true, DialogResult = DialogResult.OK };
            dialog.Controls.AddRange(new Control[] { field, ok }); dialog.AcceptButton = ok; return dialog.ShowDialog(this) == DialogResult.OK ? field.Text.Trim() : null;
        }
    }

    void CreatePlaylist() { string name = Prompt("Create playlist", "My playlist"); if (string.IsNullOrWhiteSpace(name)) return; if (library.Playlists.ContainsKey(name)) { status.Text = "That playlist already exists"; return; } library.Playlists.Add(name, new List<Track>()); SaveLibrary(); RefreshNavigation(); ShowPage("Playlist", name); }
    void AddToPlaylist()
    {
        var track = Selected(); if (track == null) return; var menu = new ContextMenuStrip { BackColor = MusicTheme.Raised, ForeColor = MusicTheme.Text, ShowImageMargin = false };
        foreach (string name in library.Playlists.Keys.OrderBy(x => x)) { string key = name; menu.Items.Add(key, null, delegate { if (!library.Playlists[key].Any(x => x.Source == track.Source)) library.Playlists[key].Add(track); SaveLibrary(); if (page == "Playlist") RenderTracks(); }); }
        if (menu.Items.Count == 0) { status.Text = "Create a playlist first"; menu.Dispose(); return; } menu.Closed += delegate { menu.Dispose(); }; menu.Show(Cursor.Position);
    }
    void RemoveSelected() { if (Selected() == null) return; int index = tracks.SelectedIndices[0]; VisibleTracks().RemoveAt(index); if (page == "Queue" && index <= queueIndex) queueIndex--; if (page != "Queue") SaveLibrary(); RenderTracks(); }
    void SaveLibrary() { if (benchmark) return; try { Directory.CreateDirectory(Path.GetDirectoryName(libraryPath)); string temp = libraryPath + ".tmp"; File.WriteAllText(temp, new JavaScriptSerializer().Serialize(library)); if (File.Exists(libraryPath)) File.Replace(temp, libraryPath, libraryPath + ".bak"); else File.Move(temp, libraryPath); status.Text = "Saved"; } catch (Exception e) { status.Text = "Could not save library: " + e.Message; } }

    async Task SearchAsync()
    {
        if (searchProcess != null || searchHintVisible || string.IsNullOrWhiteSpace(search.Text)) return; Process process = null;
        try
        {
            string query = search.Text.Trim(); ShowPage("Search", null); subtitle.Text = "Searching for “" + query + "”…"; string deno = FindTool("deno");
            process = Start(FindTool("yt-dlp"), "--ignore-config --js-runtimes " + Quote("deno:" + deno) + " --flat-playlist --dump-json --no-warnings --socket-timeout 15 --retries 1 -- " + Quote("ytsearch20:" + query), true); searchProcess = process;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); var exited = Task.Run(delegate { process.WaitForExit(); }); if (await Task.WhenAny(exited, Task.Delay(45000)) != exited) { KillTree(process); throw new TimeoutException("Search timed out. Try again."); }
            await exited; string json = await output; string errors = await error; if (closing) return; if (process.ExitCode != 0) throw new InvalidOperationException("Search failed. " + errors.Substring(0, Math.Min(160, errors.Length)));
            var found = new List<Track>(); var serializer = new JavaScriptSerializer();
            foreach (string line in json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) { var data = serializer.Deserialize<Dictionary<string, object>>(line); object id, title, artist; if (!data.TryGetValue("id", out id) || id == null) continue; data.TryGetValue("title", out title); if (!data.TryGetValue("channel", out artist)) data.TryGetValue("uploader", out artist); found.Add(new Track { Title = Convert.ToString(title), Artist = Convert.ToString(artist), Source = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(Convert.ToString(id)) }); }
            results = found; if (page == "Search") { RenderTracks(); if (found.Count == 0) subtitle.Text = "No results. Try another search."; } status.Text = "Search complete";
        }
        catch (Exception e) { if (!closing) { status.Text = e.Message; if (page == "Search") subtitle.Text = "Search unavailable. You can still play imported audio."; } }
        finally { if (process != null) { KillTree(process); process.Dispose(); } searchProcess = null; }
    }

    void RunUiCheck()
    {
        try
        {
            var track = new Track { Title = "Midnight Drive", Artist = "Sample artist", Source = "sample.wav" }; library.Tracks.Add(track); library.Playlists.Add("Night Mix", new List<Track> { track }); RefreshNavigation();
            ShowPage("Library", null); if (tracks.Items.Count != 1) throw new Exception("Library navigation failed"); ShowPage("Playlist", "Night Mix"); if (tracks.Items.Count != 1 || heading.Text != "Night Mix") throw new Exception("Playlist navigation failed");
            queue.Add(track); queue.Add(track); queueIndex = 1; ShowPage("Queue", null); tracks.Items[0].Selected = true; RemoveSelected(); if (queue.Count != 1 || queueIndex != 0) throw new Exception("Queue removal failed");
            ShowPage("Search", null); if (tracks.Items.Count != 0) throw new Exception("Search state leaked tracks"); ShowPage("Settings", null); if (actions.Controls.Count != 1 || !subtitle.Text.Contains(YTMusicLiteNative.UpdateService.CurrentVersion)) throw new Exception("Settings failed"); ShowPage("Home", null); ShowMini(); if (mini == null) throw new Exception("Mini player failed"); using (var bitmap = new Bitmap(mini.Width, mini.Height)) { mini.DrawToBitmap(bitmap, new Rectangle(Point.Empty, mini.Size)); bitmap.Save("spotify-mini.png"); } mini.Close();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save("spotify-home.png"); } Size = MinimumSize; using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save("spotify-compact.png"); }
            File.WriteAllText("ui-check.txt", "PASS: Spotify-style layout, navigation, playlists, queue, search, settings, mini player");
        }
        catch (Exception e) { File.WriteAllText("ui-check.txt", "FAIL: " + e); Environment.ExitCode = 1; } Close();
    }

    void ShowMini()
    {
        if (mini != null) { mini.Activate(); return; }
        mini = new Form { Text = "YT Music Lite · Now playing", Size = new Size(470, 205), MinimumSize = new Size(470, 205), BackColor = MusicTheme.Player, ForeColor = MusicTheme.Text, TopMost = true, Font = Font, StartPosition = FormStartPosition.CenterParent };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 2, BackColor = MusicTheme.Player }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        var art = new ArtworkBox { Dock = DockStyle.Fill, KeyText = nowPlaying.Text, Margin = new Padding(0, 0, 14, 0) }; layout.Controls.Add(art, 0, 0); layout.SetRowSpan(art, 2);
        var title = new Label { Dock = DockStyle.Fill, Text = nowPlaying.Text + "\n" + nowArtist.Text, AutoEllipsis = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = MusicTheme.Text, Padding = new Padding(4, 18, 0, 0) }; EventHandler update = delegate { title.Text = nowPlaying.Text + "\n" + nowArtist.Text; }; nowPlaying.TextChanged += update; nowArtist.TextChanged += update;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = MusicTheme.Player, WrapContents = false }; buttons.Controls.Add(IconButton("◀|", async delegate { await MoveQueue(-1); }, false)); buttons.Controls.Add(IconButton("▶", delegate { if (player == null || player.HasExited) play.PerformClick(); else pause.PerformClick(); }, true)); buttons.Controls.Add(IconButton("|▶", async delegate { await MoveQueue(1); }, false));
        layout.Controls.Add(title, 1, 0); layout.Controls.Add(buttons, 1, 1); mini.Controls.Add(layout); mini.FormClosed += delegate { nowPlaying.TextChanged -= update; nowArtist.TextChanged -= update; mini = null; }; mini.Show(this);
    }

    async Task CheckUpdatesAsync()
    {
        try { status.Text = "Checking for updates…"; var service = new YTMusicLiteNative.UpdateService(); var update = await service.CheckAsync(); if (!update.UpdateAvailable) { status.Text = update.Message; return; } if (MessageBox.Show(this, update.Message + "\n\nDownload and install it now?", "YT Music Lite update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) { status.Text = "Update available"; return; } status.Text = "Downloading update…"; var progress = new Progress<int>(delegate(int percent) { status.Text = "Downloading update… " + percent + "%"; }); var prepared = await service.PrepareAsync(update, progress); service.InstallPrepared(prepared); Close(); }
        catch (Exception e) { status.Text = "Update failed: " + e.Message; }
    }
}
