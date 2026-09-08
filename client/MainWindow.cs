using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed partial class MainWindow : Form
    {
        private readonly LibraryStore store;
        private readonly LibraryData library;
        private readonly CatalogService catalog;
        private readonly PlaybackEngine playback;
        private readonly bool automation;
        private readonly bool silentPlayback;
        private readonly string benchmarkSource;
        private readonly List<Track> queue = new List<Track>();
        private readonly Dictionary<AppPage, NavButton> navigationButtons = new Dictionary<AppPage, NavButton>();
        private readonly List<PageTarget> history = new List<PageTarget>();
        private int historyIndex = -1;
        private int queueIndex = -1;
        private AppPage currentPage;
        private Playlist currentPlaylist;
        private List<Track> searchResults = new List<Track>();
        private PlaybackSnapshot snapshot = new PlaybackSnapshot { State = PlaybackState.Stopped, Volume = 85 };

        private Panel sidebar;
        private FlowLayoutPanel playlistNavigation;
        private Panel contentHost;
        private Label heading;
        private Label subtitle;
        private TextBox searchBox;
        private IconButton searchButton;
        private IconButton backButton;
        private IconButton forwardButton;
        private FlowLayoutPanel actionBar;
        private RowStyle actionRow;
        private FlowLayoutPanel homeTiles;
        private RowStyle homeRow;
        private TrackListControl trackList;
        private Panel settingsPanel;
        private ArtworkControl playerArtwork;
        private Label playerTitle;
        private Label playerArtist;
        private Label statusLabel;
        private IconButton playPauseButton;
        private IconButton saveButton;
        private ValueSlider progressSlider;
        private ValueSlider volumeSlider;
        private Label elapsedLabel;
        private Label durationLabel;
        private MiniPlayerWindow miniPlayer;
        private NotifyIcon trayIcon;
        private bool searching;
        private bool closing;
        private bool updatingProgress;

        public MainWindow(string[] args)
        {
            automation = args.Any(item => string.Equals(item, "--ui-check", StringComparison.OrdinalIgnoreCase));
            int benchmarkIndex = Array.FindIndex(args, item => string.Equals(item, "--benchmark", StringComparison.OrdinalIgnoreCase));
            benchmarkSource = benchmarkIndex >= 0 && benchmarkIndex + 1 < args.Length ? args[benchmarkIndex + 1] : null;
            silentPlayback = automation || !string.IsNullOrEmpty(benchmarkSource) || args.Any(item => string.Equals(item, "--silent", StringComparison.OrdinalIgnoreCase));
            store = new LibraryStore();
            library = automation ? new LibraryData() : store.Load();
            catalog = new CatalogService();
            playback = new PlaybackEngine();
            playback.SnapshotChanged += PlaybackSnapshotChanged;
            playback.PlaybackEnded += PlaybackEnded;
            BuildWindow();
            BuildTray();
            RefreshPlaylistNavigation();
            Navigate(AppPage.Home, null, true);
            FormClosing += HandleFormClosing;
            KeyPreview = true;
            KeyDown += MainKeyDown;
            if (automation) Shown += delegate { BeginInvoke((Action)RunUiCheck); };
            else if (!string.IsNullOrEmpty(benchmarkSource)) Shown += delegate { BeginInvoke((Action)RunBenchmark); };
        }

        private void BuildWindow()
        {
            Text = "YT Music Lite";
            Icon = Branding.CreateIcon();
            Size = new Size(1240, 790);
            MinimumSize = new Size(920, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9);
            AutoScaleMode = AutoScaleMode.Dpi;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = Padding.Empty;
            root.Margin = Padding.Empty;
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            Controls.Add(root);

            sidebar = BuildSidebar();
            contentHost = BuildContent();
            Control playerBar = BuildPlayerBar();
            root.Controls.Add(sidebar, 0, 0);
            root.Controls.Add(contentHost, 1, 0);
            root.Controls.Add(playerBar, 0, 1);
            root.SetColumnSpan(playerBar, 2);
            Resize += delegate { root.ColumnStyles[0].Width = ClientSize.Width < 1030 ? 198 : 232; };
        }

        private Panel BuildSidebar()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Theme.Sidebar;
            panel.Padding = new Padding(8, 0, 8, 12);
            panel.Controls.Add(new BrandControl());

            Panel navigation = new Panel();
            navigation.Dock = DockStyle.Top;
            navigation.Height = 238;
            navigation.Padding = new Padding(0, 4, 0, 0);
            panel.Controls.Add(navigation);
            navigation.BringToFront();

            AddNavigation(navigation, AppPage.Settings, "Settings", AppIcon.Settings);
            AddNavigation(navigation, AppPage.Queue, "Queue", AppIcon.Queue);
            AddNavigation(navigation, AppPage.Library, "Your library", AppIcon.Library);
            AddNavigation(navigation, AppPage.Search, "Search", AppIcon.Search);
            AddNavigation(navigation, AppPage.Home, "Home", AppIcon.Home);

            Panel playlistHeader = new Panel();
            playlistHeader.Dock = DockStyle.Top;
            playlistHeader.Height = 48;
            Label label = new Label { Text = "PLAYLISTS", ForeColor = Theme.Faint, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, Padding = new Padding(14, 0, 0, 0) };
            IconButton add = new IconButton { Icon = AppIcon.Add, AccessibleName = "Create playlist", Dock = DockStyle.Right, Width = 42 };
            add.Click += delegate { CreatePlaylist(); };
            playlistHeader.Controls.Add(label);
            playlistHeader.Controls.Add(add);
            panel.Controls.Add(playlistHeader);
            playlistHeader.BringToFront();

            playlistNavigation = new FlowLayoutPanel();
            playlistNavigation.Dock = DockStyle.Fill;
            playlistNavigation.FlowDirection = FlowDirection.TopDown;
            playlistNavigation.WrapContents = false;
            playlistNavigation.AutoScroll = true;
            playlistNavigation.BackColor = Theme.Sidebar;
            panel.Controls.Add(playlistNavigation);
            playlistNavigation.BringToFront();
            return panel;
        }

        private void AddNavigation(Control parent, AppPage page, string label, AppIcon icon)
        {
            NavButton button = new NavButton { Label = label, Icon = icon, AccessibleName = label };
            button.Click += delegate { Navigate(page, null, true); };
            parent.Controls.Add(button);
            navigationButtons[page] = button;
        }

        private Panel BuildContent()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(28, 12, 28, 18) };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Theme.Window, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            actionRow = new RowStyle(SizeType.Absolute, 52);
            layout.RowStyles.Add(actionRow);
            homeRow = new RowStyle(SizeType.Absolute, 0);
            layout.RowStyles.Add(homeRow);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            host.Controls.Add(layout);

            layout.Controls.Add(BuildTopBar(), 0, 0);
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
            heading = new Label { Dock = DockStyle.Top, Height = 45, ForeColor = Theme.Text, Font = new Font("Segoe UI", 24, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
            subtitle = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9.5f), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
            header.Controls.Add(subtitle);
            header.Controls.Add(heading);
            layout.Controls.Add(header, 0, 1);

            actionBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false, BackColor = Theme.Window, Padding = new Padding(0, 6, 0, 6) };
            layout.Controls.Add(actionBar, 0, 2);

            homeTiles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, BackColor = Theme.Window, Padding = new Padding(0, 6, 0, 10) };
            layout.Controls.Add(homeTiles, 0, 3);

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
            trackList = new TrackListControl { Dock = DockStyle.Fill };
            trackList.TrackActivated += async delegate { await PlaySelectedAsync(); };
            trackList.ContextRequested += delegate { ShowTrackMenu(); };
            trackList.SelectionChanged += delegate { UpdateActionState(); };
            body.Controls.Add(trackList);
            settingsPanel = BuildSettingsPanel();
            body.Controls.Add(settingsPanel);
            layout.Controls.Add(body, 0, 4);
            host.Resize += delegate { actionRow.Height = host.ClientSize.Width < 760 ? 88 : 52; };
            return host;
        }

        private Control BuildTopBar()
        {
            TableLayoutPanel top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Theme.Window, Margin = Padding.Empty };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            FlowLayoutPanel historyButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Window, Padding = new Padding(0, 8, 0, 0) };
            backButton = new IconButton { Icon = AppIcon.Back, AccessibleName = "Back", Enabled = false };
            forwardButton = new IconButton { Icon = AppIcon.Forward, AccessibleName = "Forward", Enabled = false };
            backButton.Click += delegate { MoveHistory(-1); };
            forwardButton.Click += delegate { MoveHistory(1); };
            historyButtons.Controls.Add(backButton);
            historyButtons.Controls.Add(forwardButton);
            top.Controls.Add(historyButtons, 0, 0);

            SectionCard searchShell = new SectionCard { Dock = DockStyle.Fill, Margin = new Padding(2, 8, 12, 8), Padding = new Padding(16, 8, 10, 7) };
            searchBox = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), AccessibleName = "Search YouTube Music" };
            searchBox.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
            searchShell.Controls.Add(searchBox);
            top.Controls.Add(searchShell, 1, 0);
            searchButton = new IconButton { Icon = AppIcon.Search, Accent = true, Dock = DockStyle.Fill, Margin = new Padding(4, 8, 4, 8), AccessibleName = "Search" };
            searchButton.Click += async delegate { await SearchAsync(); };
            top.Controls.Add(searchButton, 2, 0);
            return top;
        }

        private Control BuildPlayerBar()
        {
            Panel shell = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(18, 9, 18, 8) };
            TableLayoutPanel columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Theme.Sidebar, Margin = Padding.Empty };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
            shell.Controls.Add(columns);

            TableLayoutPanel info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Theme.Sidebar };
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            playerArtwork = new ArtworkControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), Radius = 9, KeyText = "♪" };
            info.Controls.Add(playerArtwork, 0, 0); info.SetRowSpan(playerArtwork, 3);
            playerTitle = new Label { Text = "Nothing playing", Dock = DockStyle.Fill, ForeColor = Theme.Text, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
            playerArtist = new Label { Text = "Choose something from Home or Search", Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
            statusLabel = new Label { Text = "Ready", Dock = DockStyle.Fill, ForeColor = Theme.Faint, Font = new Font("Segoe UI", 8), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
            info.Controls.Add(playerTitle, 1, 0); info.Controls.Add(playerArtist, 1, 1); info.Controls.Add(statusLabel, 1, 2);
            saveButton = new IconButton { Icon = AppIcon.Heart, AccessibleName = "Save song", Dock = DockStyle.Fill };
            saveButton.Click += delegate { ToggleSaveCurrent(); };
            info.Controls.Add(saveButton, 2, 0); info.SetRowSpan(saveButton, 3);
            columns.Controls.Add(info, 0, 0);

            TableLayoutPanel center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Theme.Sidebar };
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel transport = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Sidebar, Padding = new Padding(0, 4, 0, 0) };
            IconButton previous = PlayerIcon(AppIcon.Previous, "Previous song"); previous.Click += async delegate { await MoveQueueAsync(-1); };
            playPauseButton = PlayerIcon(AppIcon.Play, "Play or pause"); playPauseButton.Accent = true; playPauseButton.Size = new Size(46, 46); playPauseButton.Click += async delegate { await TogglePlaybackAsync(); };
            IconButton next = PlayerIcon(AppIcon.Next, "Next song"); next.Click += async delegate { await MoveQueueAsync(1); };
            transport.Controls.Add(previous); transport.Controls.Add(playPauseButton); transport.Controls.Add(next);
            transport.Dock = DockStyle.None; transport.Size = new Size(150, 50);
            Panel transportCenter = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar }; transportCenter.Controls.Add(transport); transportCenter.Resize += delegate { transport.Location = new Point(Math.Max(0, (transportCenter.Width - transport.Width) / 2), 0); };
            center.Controls.Add(transportCenter, 0, 0); center.SetColumnSpan(transportCenter, 3);
            elapsedLabel = TimeLabel("0:00", ContentAlignment.MiddleRight);
            durationLabel = TimeLabel("0:00", ContentAlignment.MiddleLeft);
            progressSlider = new ValueSlider { Dock = DockStyle.Fill, AccessibleName = "Song position", Margin = new Padding(6, 0, 6, 0), Maximum = 1 };
            progressSlider.ValueCommitted += async delegate { if (!updatingProgress) await playback.SeekAsync(progressSlider.Value); };
            center.Controls.Add(elapsedLabel, 0, 1); center.Controls.Add(progressSlider, 1, 1); center.Controls.Add(durationLabel, 2, 1);
            columns.Controls.Add(center, 1, 0);

            FlowLayoutPanel utility = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Theme.Sidebar, Padding = new Padding(0, 24, 0, 0) };
            IconButton mini = PlayerIcon(AppIcon.Mini, "Open mini player"); mini.Click += delegate { ShowMiniPlayer(); };
            IconButton queueButton = PlayerIcon(AppIcon.Queue, "Show queue"); queueButton.Click += delegate { Navigate(AppPage.Queue, null, true); };
            volumeSlider = new ValueSlider { Width = 110, Value = 85, AccessibleName = "Volume", Margin = new Padding(2, 7, 4, 0) };
            volumeSlider.ValueCommitted += async delegate { await playback.SetVolumeAsync((int)volumeSlider.Value); };
            utility.Controls.Add(mini); utility.Controls.Add(queueButton); utility.Controls.Add(volumeSlider);
            IconButton volumeIcon = PlayerIcon(AppIcon.Volume, "Volume"); volumeIcon.TabStop = false; utility.Controls.Add(volumeIcon);
            columns.Controls.Add(utility, 2, 0);
            return shell;
        }

        private IconButton PlayerIcon(AppIcon icon, string name)
        {
            return new IconButton { Icon = icon, AccessibleName = name, Margin = new Padding(4, 0, 4, 0) };
        }

        private Label TimeLabel(string text, ContentAlignment alignment)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = Theme.Faint, Font = new Font("Segoe UI", 8), TextAlign = alignment };
        }

        private Panel BuildSettingsPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Visible = false, AutoScroll = true };
            FlowLayoutPanel stack = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 450, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Window };
            stack.Controls.Add(SettingsCard("Playback", "Browser-free audio", "Audio runs through mpv. The YouTube resolver starts only when a song is opened and exits immediately afterward."));
            stack.Controls.Add(SettingsCard("Memory", "Low-memory by design", "Artwork is loaded on demand, the in-memory cache is bounded, and playback buffers are capped."));
            SectionCard update = SettingsCard("Updates", "YT Music Lite 6.0.0", "Updates are downloaded from this repository and verified with SHA-256 before installation.");
            update.Height = 178;
            PillButton check = new PillButton { Label = "Check for updates", Width = 166, ShowIcon = true, Icon = AppIcon.Download, Left = 18, Top = 122 };
            check.Click += async delegate { await CheckForUpdatesAsync(); };
            update.Controls.Add(check);
            stack.Controls.Add(update);
            panel.Controls.Add(stack);
            return panel;
        }

        private SectionCard SettingsCard(string eyebrow, string title, string body)
        {
            SectionCard card = new SectionCard { Width = 700, Height = 142, Margin = new Padding(0, 0, 0, 12) };
            Label eyebrowLabel = new Label { Text = eyebrow.ToUpperInvariant(), ForeColor = Theme.Accent, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = false, Left = 18, Top = 15, Width = 650, Height = 18 };
            Label titleLabel = new Label { Text = title, ForeColor = Theme.Text, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = false, Left = 18, Top = 38, Width = 650, Height = 28 };
            Label bodyLabel = new Label { Text = body, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9), AutoSize = false, Left = 18, Top = 69, Width = 650, Height = 48 };
            card.Controls.Add(eyebrowLabel); card.Controls.Add(titleLabel); card.Controls.Add(bodyLabel);
            return card;
        }

        private void BuildTray()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Text = "YT Music Lite";
            trayIcon.Icon = Icon;
            ContextMenuStrip menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false };
            menu.Items.Add("Show YT Music Lite", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add("Play / pause", null, async delegate { await TogglePlaybackAsync(); });
            menu.Items.Add("Next song", null, async delegate { await MoveQueueAsync(1); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { closing = true; Close(); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
            trayIcon.Visible = true;
        }

        private sealed class PageTarget
        {
            public AppPage Page;
            public string PlaylistId;
        }
    }
}
