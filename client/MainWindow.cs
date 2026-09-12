using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed partial class MainWindow : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr parameter, string text);
        private const int SetCueBanner = 0x1501;
        private readonly LibraryStore store;
        private readonly LibraryData library;
        private readonly SettingsStore settingsStore;
        private readonly ClientSettings clientSettings;
        private readonly CatalogService catalog;
        private readonly PlaybackEngine playback;
        private readonly bool automation;
        private readonly bool silentPlayback;
        private readonly string benchmarkSource;
        private readonly List<Track> queue = new List<Track>();
        private readonly HashSet<string> loadingPlaylists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AppPage, NavButton> navigationButtons = new Dictionary<AppPage, NavButton>();
        private readonly List<PageTarget> history = new List<PageTarget>();
        private int historyIndex = -1;
        private int queueIndex = -1;
        private AppPage currentPage;
        private Playlist currentPlaylist;
        private List<Track> searchResults = new List<Track>();
        private List<Track> discoveryResults = new List<Track>();
        private bool discovering;
        private bool discoveryAutoAttempted;
        private PlaybackSnapshot snapshot = new PlaybackSnapshot { State = PlaybackState.Stopped, Volume = 85 };
        private readonly Random random = new Random();
        private bool shuffleEnabled;
        private RepeatMode repeatMode;
        private int lastAudibleVolume = 85;
        private int currentVolume = 85;

        private Panel sidebar;
        private FlowLayoutPanel playlistNavigation;
        private Panel contentHost;
        private Label heading;
        private Label subtitle;
        private TextBox searchBox;
        private IconButton backButton;
        private IconButton forwardButton;
        private FlowLayoutPanel actionBar;
        private RowStyle actionRow;
        private FlowLayoutPanel homeTiles;
        private RowStyle homeRow;
        private TrackListControl trackList;
        private FlowLayoutPanel playlistGrid;
        private Panel settingsPanel;
        private Label youtubeAccessTitle;
        private Label youtubeAccessBody;
        private ArtworkControl playerArtwork;
        private Label playerTitle;
        private Label playerArtist;
        private Label statusLabel;
        private IconButton playPauseButton;
        private IconButton shuffleButton;
        private IconButton repeatButton;
        private IconButton stopButton;
        private IconButton muteButton;
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
        private bool accountSyncing;

        public MainWindow(string[] args)
        {
            automation = args.Any(item => string.Equals(item, "--ui-check", StringComparison.OrdinalIgnoreCase));
            int benchmarkIndex = Array.FindIndex(args, item => string.Equals(item, "--benchmark", StringComparison.OrdinalIgnoreCase));
            benchmarkSource = benchmarkIndex >= 0 && benchmarkIndex + 1 < args.Length ? args[benchmarkIndex + 1] : null;
            silentPlayback = automation || !string.IsNullOrEmpty(benchmarkSource) || args.Any(item => string.Equals(item, "--silent", StringComparison.OrdinalIgnoreCase));
            store = new LibraryStore();
            library = automation ? new LibraryData() : store.Load();
            discoveryResults = new List<Track>(library.DiscoveryTracks);
            settingsStore = new SettingsStore();
            clientSettings = automation ? new ClientSettings() : settingsStore.Load();
            clientSettings.Volume = Math.Max(0, Math.Min(100, clientSettings.Volume));
            currentVolume = clientSettings.Volume;
            shuffleEnabled = clientSettings.Shuffle;
            repeatMode = ParseRepeatMode(clientSettings.RepeatMode);
            lastAudibleVolume = clientSettings.Volume > 0 ? clientSettings.Volume : 85;
            snapshot.Volume = clientSettings.Volume;
            catalog = new CatalogService(clientSettings);
            playback = new PlaybackEngine(clientSettings);
            playback.SnapshotChanged += PlaybackSnapshotChanged;
            playback.PlaybackEnded += PlaybackEnded;
            BuildWindow();
            ApplyPlaybackOptions();
            BuildTray();
            RefreshPlaylistNavigation();
            Navigate(AppPage.Home, null, true);
            FormClosing += HandleFormClosing;
            KeyPreview = true;
            KeyDown += MainKeyDown;
            if (automation) Shown += delegate { BeginInvoke((Action)RunUiCheck); };
            else if (!string.IsNullOrEmpty(benchmarkSource)) Shown += delegate { BeginInvoke((Action)RunBenchmark); };
            else if (clientSettings.AccessVerified && clientSettings.LastAccountSyncUtc < DateTime.UtcNow.AddHours(-6)) Shown += async delegate { await SyncAccountAsync(false, true); };
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
            navigation.Height = 330;
            navigation.Padding = new Padding(0, 4, 0, 0);
            panel.Controls.Add(navigation);
            navigation.BringToFront();

            AddNavigation(navigation, AppPage.Settings, "Settings", AppIcon.Settings);
            AddNavigation(navigation, AppPage.Queue, "Queue", AppIcon.Queue);
            AddNavigation(navigation, AppPage.Playlists, "Playlists", AppIcon.Playlist);
            AddNavigation(navigation, AppPage.Library, "Your library", AppIcon.Library);
            AddNavigation(navigation, AppPage.Search, "Search", AppIcon.Search);
            AddNavigation(navigation, AppPage.Discover, "Discover", AppIcon.Discover);
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
            playlistGrid = new FlowLayoutPanel { Dock = DockStyle.Fill, Visible = false, AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Theme.Window, Padding = new Padding(0, 8, 0, 8) };
            body.Controls.Add(playlistGrid);
            settingsPanel = BuildSettingsPanel();
            body.Controls.Add(settingsPanel);
            layout.Controls.Add(body, 0, 4);
            host.Resize += delegate { actionRow.Height = host.ClientSize.Width < 760 ? 88 : 52; };
            return host;
        }

        private Control BuildTopBar()
        {
            TableLayoutPanel top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Window, Margin = Padding.Empty };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            FlowLayoutPanel historyButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Window, Padding = new Padding(0, 8, 0, 0) };
            backButton = new IconButton { Icon = AppIcon.Back, AccessibleName = "Back", Enabled = false };
            forwardButton = new IconButton { Icon = AppIcon.Forward, AccessibleName = "Forward", Enabled = false };
            backButton.Click += delegate { MoveHistory(-1); };
            forwardButton.Click += delegate { MoveHistory(1); };
            historyButtons.Controls.Add(backButton);
            historyButtons.Controls.Add(forwardButton);
            top.Controls.Add(historyButtons, 0, 0);

            SectionCard searchShell = new SectionCard { Dock = DockStyle.Fill, Margin = new Padding(2, 8, 0, 8), Padding = new Padding(16, 8, 10, 7) };
            searchBox = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), AccessibleName = "Search YouTube Music" };
            searchBox.HandleCreated += delegate { SendMessage(searchBox.Handle, SetCueBanner, (IntPtr)1, "Search songs or artists"); };
            searchBox.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
            searchShell.Controls.Add(searchBox);
            top.Controls.Add(searchShell, 1, 0);
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
            saveButton = new IconButton { Icon = AppIcon.Heart, AccessibleName = "Save song", Anchor = AnchorStyles.None, Margin = Padding.Empty };
            saveButton.Click += delegate { ToggleSaveCurrent(); };
            info.Controls.Add(saveButton, 2, 0); info.SetRowSpan(saveButton, 3);
            columns.Controls.Add(info, 0, 0);

            TableLayoutPanel center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Theme.Sidebar };
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46)); center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            FlowLayoutPanel transport = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Sidebar, Padding = new Padding(0, 4, 0, 0) };
            shuffleButton = PlayerIcon(AppIcon.Shuffle, "Shuffle"); shuffleButton.Checked = shuffleEnabled; shuffleButton.Click += delegate { ToggleShuffle(); };
            IconButton previous = PlayerIcon(AppIcon.Previous, "Previous song"); previous.Click += async delegate { await MoveQueueAsync(-1); };
            playPauseButton = PlayerIcon(AppIcon.Play, "Play or pause"); playPauseButton.Accent = true; playPauseButton.Size = new Size(46, 46); playPauseButton.Click += async delegate { await TogglePlaybackAsync(); };
            IconButton next = PlayerIcon(AppIcon.Next, "Next song"); next.Click += async delegate { await MoveQueueAsync(1); };
            repeatButton = PlayerIcon(repeatMode == RepeatMode.One ? AppIcon.RepeatOne : AppIcon.Repeat, "Repeat"); repeatButton.Checked = repeatMode != RepeatMode.Off; repeatButton.Click += delegate { CycleRepeatMode(); };
            stopButton = PlayerIcon(AppIcon.Stop, "Stop"); stopButton.Click += delegate { StopPlayback(); };
            transport.Controls.Add(shuffleButton); transport.Controls.Add(previous); transport.Controls.Add(playPauseButton); transport.Controls.Add(next); transport.Controls.Add(repeatButton); transport.Controls.Add(stopButton);
            transport.Dock = DockStyle.None; transport.Size = new Size(286, 50);
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
            volumeSlider = new ValueSlider { Width = 110, Value = clientSettings.Volume, AccessibleName = "Volume", Margin = new Padding(2, 7, 4, 0) };
            volumeSlider.ValueCommitted += async delegate { await SetVolumeAsync((int)volumeSlider.Value); };
            utility.Controls.Add(mini); utility.Controls.Add(queueButton); utility.Controls.Add(volumeSlider);
            muteButton = PlayerIcon(clientSettings.Volume == 0 ? AppIcon.VolumeMuted : AppIcon.Volume, "Mute or unmute"); muteButton.Click += async delegate { await ToggleMuteAsync(); }; utility.Controls.Add(muteButton);
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
            FlowLayoutPanel stack = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 392, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Window };
            SectionCard access = SettingsCard("YouTube sign-in", YtDlpOptions.FriendlyName(clientSettings), clientSettings.CookieSource == "none" ? "Choose a browser below. YT Music Lite will open a separate YouTube Music sign-in window." : (clientSettings.AccessVerified ? "The authenticated session has been verified and is ready for playback." : "This browser session has not been verified yet. Sign in again to complete verification."));
            access.Height = 142;
            youtubeAccessTitle = access.Controls.OfType<Label>().ElementAt(1);
            youtubeAccessBody = access.Controls.OfType<Label>().ElementAt(2);
            FlowLayoutPanel accessButtons = new FlowLayoutPanel { Left = 14, Top = 102, Width = 670, Height = 38, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Surface };
            accessButtons.Controls.Add(SettingsButton("Edge", delegate { SetBrowserAccess("edge"); }));
            accessButtons.Controls.Add(SettingsButton("Brave", delegate { SetBrowserAccess("brave"); }));
            accessButtons.Controls.Add(SettingsButton("Chrome", delegate { SetBrowserAccess("chrome"); }));
            accessButtons.Controls.Add(SettingsButton("Firefox", delegate { SetBrowserAccess("firefox"); }));
            accessButtons.Controls.Add(SettingsButton("cookies.txt", ImportCookies));
            accessButtons.Controls.Add(SettingsButton("Sync", SyncAccount));
            accessButtons.Controls.Add(SettingsButton("Clear", ClearAccess));
            access.Controls.Add(accessButtons);
            stack.Controls.Add(access);
            stack.Controls.Add(SettingsCard("Playback and memory", "Native audio, bounded resources", "mpv runs without video, resolver processes exit after each lookup, playback buffers are capped, and artwork caching is bounded."));
            SectionCard update = SettingsCard("Updates", "YT Music Lite 7.2.1", "Updates are downloaded from this repository and verified with SHA-256 before installation.");
            update.Height = 130;
            update.Margin = Padding.Empty;
            PillButton check = new PillButton { Label = "Check for updates", Width = 166, ShowIcon = true, Icon = AppIcon.Download, Left = 18, Top = 92 };
            check.Click += async delegate { await CheckForUpdatesAsync(); };
            update.Controls.Add(check);
            stack.Controls.Add(update);
            panel.Controls.Add(stack);
            return panel;
        }

        private PillButton SettingsButton(string label, Action action)
        {
            PillButton button = new PillButton { Label = label, Width = label == "cookies.txt" ? 106 : 80, Height = 36, Margin = new Padding(4, 0, 4, 0), AccessibleName = label };
            button.Click += delegate { action(); };
            return button;
        }

        private SectionCard SettingsCard(string eyebrow, string title, string body)
        {
            SectionCard card = new SectionCard { Width = 700, Height = 96, Margin = new Padding(0, 0, 0, 12) };
            Label eyebrowLabel = new Label { Text = eyebrow.ToUpperInvariant(), ForeColor = Theme.Accent, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = false, Left = 18, Top = 15, Width = 650, Height = 18 };
            Label titleLabel = new Label { Text = title, ForeColor = Theme.Text, Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = false, Left = 18, Top = 38, Width = 650, Height = 28 };
            Label bodyLabel = new Label { Text = body, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9), AutoSize = false, Left = 18, Top = 69, Width = 650, Height = 24 };
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
            menu.Items.Add("Previous song", null, async delegate { await MoveQueueAsync(-1); });
            menu.Items.Add("Next song", null, async delegate { await MoveQueueAsync(1); });
            menu.Items.Add("Stop", null, delegate { StopPlayback(); });
            menu.Items.Add("Shuffle", null, delegate { ToggleShuffle(); });
            menu.Items.Add("Cycle repeat", null, delegate { CycleRepeatMode(); });
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
