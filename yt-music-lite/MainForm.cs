using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YTMusicLite
{
    public sealed class MainForm : Form
    {
        private readonly WebView2 web;
        private readonly Panel topBar;
        private readonly Panel playerBar;
        private readonly Label status;
        private readonly LiteButton updateBadge;
        private readonly PictureBox artwork;
        private readonly Label nowTitle;
        private readonly Label nowArtist;
        private readonly Label timeLabel;
        private readonly LiteButton playPauseButton;
        private readonly SeekBar progress;
        private readonly SeekBar volume;
        private readonly Timer stateTimer;
        private readonly NotifyIcon tray;
        private readonly MiniPlayerForm miniPlayer;
        private readonly JavaScriptSerializer json;
        private readonly UpdateService updateService;
        private readonly string settingsPath;
        private readonly ToolStripMenuItem minimizeToTrayItem;
        private readonly ToolTip tips;

        private readonly Panel statePanel;
        private readonly Label stateTitle;
        private readonly Label stateDescription;
        private readonly LiteButton stateAction;
        private readonly LiteButton backButton;
        private readonly LiteButton forwardButton;
        private Action recoveryAction;
        private bool initializing;
        private bool refreshing;
        private bool suspending;
        private int wakeGeneration;
        private bool trayExplained;
        private bool miniAlwaysOnTop = true;
        private Point? miniLocation;
        private SettingsForm settingsWindow;
        private bool updateDownloading;
        private PreparedUpdate preparedUpdate;
        public event EventHandler UpdateChanged;
        public string UpdateMessage { get; private set; }
        public string UpdateNotes { get; private set; }
        public int UpdateProgress { get; private set; }
        public bool UpdateBusy { get { return updateCheckRunning || updateDownloading; } }
        public bool UpdateAvailable { get { return pendingUpdate != null; } }
        public bool UpdateReady { get { return preparedUpdate != null; } }
        public bool MiniAlwaysOnTop { get { return miniAlwaysOnTop; } }
        public Point? MiniLocation { get { return miniLocation; } }

        private PlayerState lastState;
        private UpdateCheckResult pendingUpdate;
        private bool initialized;
        private bool exiting;
        private bool autoSuspended;
        private bool manualSleep;
        private bool updateCheckRunning;
        private bool minimizeToTray;
        private bool closeToTray;
        private bool automaticUpdateChecks;
        private string artworkUrl = "";

        public bool IsExiting { get { return exiting; } }
        public bool MinimizeToTrayEnabled { get { return minimizeToTray; } }
        public bool CloseToTrayEnabled { get { return closeToTray; } }
        public bool AutomaticUpdateChecksEnabled { get { return automaticUpdateChecks; } }

        public MainForm() : this(true) { }

        internal MainForm(bool initializeWebView)
        {
            AutoScaleDimensions = new SizeF(6f, 13f);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "YT Music Lite";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            Size = new Size(1280, 820);
            BackColor = Color.FromArgb(10, 10, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Icon = BrandArt.CreateIcon();

            json = new JavaScriptSerializer();
            lastState = new PlayerState();
            updateService = new UpdateService();
            tips = new ToolTip();
            tips.ShowAlways = true;

            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YTMusicLite",
                "settings.ini");
            LoadSettings();
            UpdateMessage = "Check for a newer version when you are ready.";
            UpdateNotes = "";

            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 52;
            topBar.BackColor = Color.FromArgb(20, 21, 25);

            int navX = 165;
            LiteButton back = backButton = MakeTopButton("‹", navX, "Back");
            back.Click += delegate { if (initialized && web.CanGoBack) { WakeWebView(); web.GoBack(); } };
            navX += 38;
            LiteButton forward = forwardButton = MakeTopButton("›", navX, "Forward");
            forward.Click += delegate { if (initialized && web.CanGoForward) { WakeWebView(); web.GoForward(); } };
            back.Enabled = forward.Enabled = false;
            navX += 38;
            LiteButton reload = MakeTopButton("↻", navX, "Reload");
            reload.Click += delegate { if (initialized) { WakeWebView(); web.Reload(); } };
            navX += 38;
            LiteButton home = MakeTopButton("⌂", navX, "YouTube Music home");
            home.Click += delegate { NavigateHome(); };

            status = new Label();
            status.Text = "";
            status.ForeColor = Color.FromArgb(125, 125, 125);
            status.Location = new Point(navX + 46, 18);
            status.Size = new Size(220, 20);
            status.AutoEllipsis = true;
            topBar.Controls.Add(status);

            LiteButton settingsButton = MakeTopButton("⚙", 0, "Settings");
            settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            settingsButton.Location = new Point(ClientSize.Width - 48, 9);
            settingsButton.Click += delegate { ShowSettings(); };

            LiteButton miniButton = MakeTopButton("▱", 0, "Mini player");
            miniButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            miniButton.Location = new Point(ClientSize.Width - 90, 9);
            miniButton.Click += delegate { ShowMiniPlayer(); };
            miniButton.Text = "Mini player";
            miniButton.DrawnIcon = IconKind.MiniPlayer;
            miniButton.ShowIconCaption = true;
            miniButton.IconStyle = IconButtonStyle.Soft;
            miniButton.Font = new Font("Segoe UI", 9.5f);
            miniButton.Size = new Size(122, 34);

            updateBadge = new LiteButton();
            updateBadge.AccessibleName = "App updates";
            updateBadge.Text = "Update";
            updateBadge.Visible = false;
            updateBadge.AutoSize = false;
            updateBadge.Size = new Size(66, 24);
            updateBadge.TextAlign = ContentAlignment.MiddleCenter;
            updateBadge.ForeColor = Color.White;
            updateBadge.BackColor = Color.FromArgb(205, 35, 55);
            updateBadge.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            updateBadge.Cursor = Cursors.Hand;
            updateBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            updateBadge.Location = new Point(ClientSize.Width - 164, 15);
            updateBadge.Click += delegate { ShowSettings(true); };
            tips.SetToolTip(updateBadge, "A new YT Music Lite version is available");
            topBar.Controls.Add(updateBadge);

            TableLayoutPanel toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Padding = new Padding(14, 6, 12, 6) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            BrandLogoControl logo = new BrandLogoControl { Dock = DockStyle.Fill, Cursor = Cursors.Default };
            toolbar.Controls.Add(logo, 0, 0);
            FlowLayoutPanel navigation = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty, Padding = new Padding(3, 0, 3, 0), BackColor = Color.FromArgb(28, 30, 35) };
            foreach (LiteButton button in new LiteButton[] { back, forward, reload, home })
            {
                button.Margin = new Padding(1, 0, 1, 0);
                navigation.Controls.Add(button);
            }
            toolbar.Controls.Add(navigation, 1, 0);
            status.Margin = new Padding(14, 0, 6, 0);
            status.Dock = DockStyle.Fill;
            status.TextAlign = ContentAlignment.MiddleLeft;
            toolbar.Controls.Add(status, 2, 0);
            updateBadge.Anchor = miniButton.Anchor = settingsButton.Anchor = AnchorStyles.None;
            toolbar.Controls.Add(updateBadge, 3, 0);
            toolbar.Controls.Add(miniButton, 4, 0);
            settingsButton.Margin = new Padding(8, 0, 0, 0);
            toolbar.Controls.Add(settingsButton, 5, 0);
            topBar.Controls.Add(toolbar);
            Panel toolbarLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(39, 41, 47) };
            topBar.Controls.Add(toolbarLine);

            web = new WebView2();
            web.Dock = DockStyle.Fill;
            web.BackColor = Color.Black;

            Panel webHost = new Panel();
            webHost.Dock = DockStyle.Fill;
            webHost.BackColor = Color.Black;
            webHost.Padding = new Padding(0, 1, 0, 0);
            webHost.Controls.Add(web);

            statePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 18, 18), Padding = new Padding(36), Visible = false };
            TableLayoutPanel stateLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            stateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            stateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            stateTitle = new Label { Text = "Opening YouTube Music", AutoSize = true, Anchor = AnchorStyles.None, Font = new Font("Segoe UI", 18f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) };
            stateDescription = new Label { AutoSize = true, Anchor = AnchorStyles.None, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(175, 175, 175), MaximumSize = new Size(560, 0), Margin = new Padding(0, 0, 0, 20) };
            stateAction = new LiteButton { Text = "Retry", AutoSize = true, Anchor = AnchorStyles.None, Padding = new Padding(18, 8, 18, 8) };
            stateAction.Click += delegate { if (recoveryAction != null) recoveryAction(); };
            stateLayout.Controls.Add(stateTitle, 0, 1);
            stateLayout.Controls.Add(stateDescription, 0, 2);
            stateLayout.Controls.Add(stateAction, 0, 3);
            statePanel.Controls.Add(stateLayout);
            webHost.Controls.Add(statePanel);

            playerBar = new Panel();
            playerBar.Dock = DockStyle.Bottom;
            playerBar.Height = 88;
            playerBar.BackColor = Color.FromArgb(17, 17, 17);

            Panel playerDivider = new Panel();
            playerDivider.Dock = DockStyle.Top;
            playerDivider.Height = 1;
            playerDivider.BackColor = Color.FromArgb(42, 42, 42);
            playerBar.Controls.Add(playerDivider);

            artwork = new PictureBox();
            artwork.Location = new Point(14, 14);
            artwork.Size = new Size(60, 60);
            artwork.SizeMode = PictureBoxSizeMode.Zoom;
            artwork.BackColor = Color.FromArgb(31, 31, 31);
            playerBar.Controls.Add(artwork);

            nowTitle = new Label();
            nowTitle.Text = "Nothing playing";
            nowTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            nowTitle.ForeColor = Color.White;
            nowTitle.AutoEllipsis = true;
            nowTitle.Location = new Point(88, 14);
            nowTitle.Size = new Size(245, 22);
            playerBar.Controls.Add(nowTitle);

            nowArtist = new Label();
            nowArtist.Text = "YouTube Music";
            nowArtist.ForeColor = Color.FromArgb(158, 158, 158);
            nowArtist.AutoEllipsis = true;
            nowArtist.Location = new Point(88, 38);
            nowArtist.Size = new Size(245, 20);
            playerBar.Controls.Add(nowArtist);

            timeLabel = new Label();
            timeLabel.Text = "0:00 / 0:00";
            timeLabel.ForeColor = Color.FromArgb(118, 118, 118);
            timeLabel.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            timeLabel.Location = new Point(88, 60);
            timeLabel.Size = new Size(110, 18);
            playerBar.Controls.Add(timeLabel);

            LiteButton previous = MakePlayerButton("⏮", "Previous");
            previous.Click += delegate { PreviousTrack(); };
            playerBar.Controls.Add(previous);

            playPauseButton = MakePlayerButton("▶", "Play / Pause");
            playPauseButton.Size = new Size(46, 42);
            playPauseButton.BackColor = Color.White;
            playPauseButton.ForeColor = Color.Black;
            playPauseButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 230, 230);
            playPauseButton.Click += delegate { TogglePlayback(); };
            playerBar.Controls.Add(playPauseButton);

            LiteButton next = MakePlayerButton("⏭", "Next");
            next.Click += delegate { NextTrack(); };
            playerBar.Controls.Add(next);

            previous.Tag = "prev";
            playPauseButton.Tag = "play";
            next.Tag = "next";

            progress = new SeekBar();
            progress.Location = new Point(350, 62);
            progress.Size = new Size(430, 18);
            progress.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            progress.SeekRequested += delegate { SeekToRatio(progress.Ratio); };
            playerBar.Controls.Add(progress);

            Label volumeIcon = new Label();
            volumeIcon.Text = "🔊";
            volumeIcon.TextAlign = ContentAlignment.MiddleCenter;
            volumeIcon.ForeColor = Color.FromArgb(185, 185, 185);
            volumeIcon.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            volumeIcon.Location = new Point(ClientSize.Width - 190, 31);
            volumeIcon.Size = new Size(28, 24);
            playerBar.Controls.Add(volumeIcon);

            volume = new SeekBar();
            volume.Ratio = 1;
            volume.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            volume.Location = new Point(ClientSize.Width - 158, 34);
            volume.Size = new Size(124, 18);
            volume.SeekRequested += delegate { SetVolume(volume.Ratio); };
            playerBar.Controls.Add(volume);

            Controls.Add(webHost);
            Controls.Add(playerBar);
            Controls.Add(topBar);

            miniPlayer = new MiniPlayerForm(this);

            stateTimer = new Timer();
            stateTimer.Interval = 850;
            stateTimer.Tick += async delegate { await RefreshPlayerStateAsync(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(28, 28, 28);
            menu.ForeColor = Color.White;
            menu.RenderMode = ToolStripRenderMode.System;
            menu.Items.Add("Show YT Music Lite", null, delegate { RestoreMainWindow(); });
            menu.Items.Add("Mini player", null, delegate { ShowMiniPlayer(); });
            menu.Items.Add("Play / Pause", null, delegate { TogglePlayback(); });
            menu.Items.Add("Pause and sleep", null, delegate { SleepNow(); });
            menu.Items.Add(new ToolStripSeparator());
            minimizeToTrayItem = new ToolStripMenuItem("Minimize to tray");
            minimizeToTrayItem.CheckOnClick = true;
            minimizeToTrayItem.Checked = minimizeToTray;
            minimizeToTrayItem.CheckedChanged += delegate { SetMinimizeToTray(minimizeToTrayItem.Checked); };
            menu.Items.Add(minimizeToTrayItem);
            menu.Items.Add("Settings", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitApplication(); });

            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "YT Music Lite";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { RestoreMainWindow(); };

            playerBar.Resize += delegate { LayoutPlayerControls(); };
            Resize += async delegate
            {
                await HandleWindowStateAsync();
            };
            Load += async delegate
            {
                LayoutPlayerControls();
                if (initializeWebView)
                {
                    await InitializeWebViewAsync();
                    if (automaticUpdateChecks) CheckForUpdatesInteractive();
                }
            };
            FormClosing += MainFormClosing;
        }

        private LiteButton MakeTopButton(string text, int x, string tip)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.Font = new Font("Segoe UI Symbol", 13f, FontStyle.Regular);
            button.Location = new Point(x, 9);
            button.Size = new Size(34, 34);
            topBar.Controls.Add(button);
            button.AccessibleName = tip;
            tips.SetToolTip(button, tip);
            return button;
        }

        private LiteButton MakePlayerButton(string text, string tip)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.Font = new Font("Segoe UI Symbol", 12f, FontStyle.Regular);
            button.Size = new Size(40, 38);
            button.AccessibleName = tip;
            tips.SetToolTip(button, tip);
            return button;
        }

        private void LayoutPlayerControls()
        {
            Control previous = FindPlayerControl("prev");
            Control play = FindPlayerControl("play");
            Control next = FindPlayerControl("next");
            if (previous == null || play == null || next == null) return;

            int center = playerBar.ClientSize.Width / 2;
            previous.Location = new Point(center - 76, 18);
            play.Location = new Point(center - 23, 16);
            next.Location = new Point(center + 36, 18);

            int progressLeft = Math.Max(350, center - 210);
            int progressRight = Math.Max(progressLeft + 80, playerBar.ClientSize.Width - 215);
            progress.Location = new Point(progressLeft, 62);
            progress.Width = Math.Max(80, progressRight - progressLeft);
        }

        private Control FindPlayerControl(string tag)
        {
            foreach (Control control in playerBar.Controls)
            {
                if (control.Tag != null && string.Equals(Convert.ToString(control.Tag), tag, StringComparison.Ordinal)) return control;
            }
            return null;
        }

        private void LoadSettings()
        {
            minimizeToTray = true;
            closeToTray = true;
            automaticUpdateChecks = true;
            miniAlwaysOnTop = true;
            try
            {
                if (!File.Exists(settingsPath)) return;
                string[] lines = File.ReadAllLines(settingsPath);
                int? miniX = null;
                int? miniY = null;
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim().ToLowerInvariant();
                    string value = line.Substring(split + 1).Trim();
                    bool enabled = value != "0";
                    if (key == "minimize_to_tray") minimizeToTray = enabled;
                    else if (key == "close_to_tray") closeToTray = enabled;
                    else if (key == "automatic_update_checks") automaticUpdateChecks = enabled;
                    else if (key == "tray_explained") trayExplained = enabled;
                    else if (key == "mini_always_on_top") miniAlwaysOnTop = enabled;
                    else if (key == "mini_x") { int x; if (int.TryParse(value, out x)) miniX = x; }
                    else if (key == "mini_y") { int y; if (int.TryParse(value, out y)) miniY = y; }
                }
                if (miniX.HasValue && miniY.HasValue) miniLocation = new Point(miniX.Value, miniY.Value);
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string content =
                    "minimize_to_tray=" + (minimizeToTray ? "1" : "0") + Environment.NewLine +
                    "close_to_tray=" + (closeToTray ? "1" : "0") + Environment.NewLine +
                    "automatic_update_checks=" + (automaticUpdateChecks ? "1" : "0") + Environment.NewLine +
                    "tray_explained=" + (trayExplained ? "1" : "0") + Environment.NewLine +
                    "mini_always_on_top=" + (miniAlwaysOnTop ? "1" : "0") + Environment.NewLine;
                if (miniLocation.HasValue) content += "mini_x=" + miniLocation.Value.X + Environment.NewLine + "mini_y=" + miniLocation.Value.Y + Environment.NewLine;
                File.WriteAllText(settingsPath, content);
            }
            catch
            {
            }
        }

        public void SetMinimizeToTray(bool value)
        {
            minimizeToTray = value;
            if (minimizeToTrayItem != null && minimizeToTrayItem.Checked != value) minimizeToTrayItem.Checked = value;
            SaveSettings();
        }

        public void SetCloseToTray(bool value)
        {
            closeToTray = value;
            SaveSettings();
        }

        public void SetAutomaticUpdateChecks(bool value)
        {
            automaticUpdateChecks = value;
            SaveSettings();
        }

        public void SetMiniAlwaysOnTop(bool value)
        {
            miniAlwaysOnTop = value;
            miniPlayer.TopMost = value;
            SaveSettings();
        }

        public void RememberMiniLocation(Point location) { miniLocation = location; SaveSettings(); }

        private void ShowState(string title, string description, string action, Action recover)
        {
            web.Visible = false;
            stateTitle.Text = title;
            stateDescription.Text = description;
            stateAction.Text = action ?? "";
            stateAction.AccessibleName = action;
            stateAction.Visible = recover != null;
            recoveryAction = recover;
            statePanel.Visible = true;
            statePanel.BringToFront();
            if (recover != null && Visible) stateAction.Focus();
        }

        private void HideState() { statePanel.Visible = false; web.Visible = true; recoveryAction = null; }

        private void UpdateNavigation()
        {
            backButton.Enabled = initialized && web.CanGoBack;
            forwardButton.Enabled = initialized && web.CanGoForward;
        }

        private async Task InitializeWebViewAsync()
        {
            if (initializing || initialized) return;
            initializing = true;
            ShowState("Opening YouTube Music", "Getting your music ready…", null, null);
            try
            {
                string profile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YTMusicLite",
                    "WebView2Profile");
                Directory.CreateDirectory(profile);

                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = "--renderer-process-limit=2 --autoplay-policy=no-user-gesture-required";
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, profile, options);
                await web.EnsureCoreWebView2Async(environment);

                web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                web.CoreWebView2.Settings.IsZoomControlEnabled = true;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;

                web.CoreWebView2.NewWindowRequested += delegate(object sender, CoreWebView2NewWindowRequestedEventArgs e)
                {
                    e.Handled = true;
                    web.CoreWebView2.Navigate(e.Uri);
                };
                web.CoreWebView2.HistoryChanged += delegate { UpdateNavigation(); };
                web.CoreWebView2.NavigationStarting += delegate
                {
                    status.Text = "Loading…";
                    HideState();
                };
                web.CoreWebView2.NavigationCompleted += async delegate(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    status.Text = "";
                    UpdateNavigation();
                    if (e.IsSuccess) { HideState(); await InjectLiteModeAsync(); }
                    else if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                        ShowState("Couldn’t load your music", "Check your connection, then try again.", "Retry", delegate { WakeWebView(); web.Reload(); });
                };

                initialized = true;
                stateTimer.Start();
                NavigateHome();
            }
            catch (Exception ex)
            {
                ShowState("Couldn’t open YouTube Music", "Try again. If this keeps happening, run the included install-webview2-runtime.cmd and reopen the app.", "Retry", async delegate { await InitializeWebViewAsync(); });
                tips.SetToolTip(stateDescription, ex.Message);
            }
            finally { initializing = false; }
        }

        private void NavigateHome()
        {
            if (!initialized) return;
            WakeWebView();
            web.CoreWebView2.Navigate("https://music.youtube.com/");
        }

        private async Task InjectLiteModeAsync()
        {
            if (!initialized) return;
            string script = @"(() => {
                if (document.getElementById('ytmlite-style')) return;
                const style = document.createElement('style');
                style.id = 'ytmlite-style';
                style.textContent = `
                    *, *::before, *::after {
                        animation-duration: 0.001ms !important;
                        animation-iteration-count: 1 !important;
                        transition-duration: 0.001ms !important;
                        scroll-behavior: auto !important;
                    }
                    ytmusic-player-page video {
                        max-height: 1px !important;
                        opacity: 0.01 !important;
                    }
                `;
                document.documentElement.appendChild(style);
            })();";
            try { await web.CoreWebView2.ExecuteScriptAsync(script); }
            catch { }
        }

        private async Task RefreshPlayerStateAsync()
        {
            if (!initialized || autoSuspended || manualSleep || refreshing || web.CoreWebView2 == null) return;
            refreshing = true;
            string script = @"(() => {
                const media = document.querySelector('video, audio');
                const titleNode = document.querySelector('ytmusic-player-bar .title, ytmusic-player-bar yt-formatted-string.title');
                const artistNode = document.querySelector('ytmusic-player-bar .byline, ytmusic-player-bar .subtitle');
                const artNode = document.querySelector('ytmusic-player-bar img, ytmusic-player-bar yt-img-shadow img');
                const title = titleNode ? titleNode.textContent.trim() : document.title.replace(/\s*-\s*YouTube Music\s*$/, '');
                const artist = artistNode ? artistNode.textContent.trim() : 'YouTube Music';
                const art = artNode && artNode.src ? artNode.src : '';
                const paused = !media || media.paused;
                const current = media && Number.isFinite(media.currentTime) ? media.currentTime : 0;
                const duration = media && Number.isFinite(media.duration) ? media.duration : 0;
                const volume = media && Number.isFinite(media.volume) ? media.volume : 1;
                return [encodeURIComponent(title), encodeURIComponent(artist), encodeURIComponent(art), paused ? '1' : '0', current, duration, volume].join('|');
            })();";

            try
            {
                string raw = await web.CoreWebView2.ExecuteScriptAsync(script);
                string decoded = json.Deserialize<string>(raw);
                if (string.IsNullOrEmpty(decoded)) return;
                string[] parts = decoded.Split('|');
                if (parts.Length < 7) return;

                PlayerState state = new PlayerState();
                state.Title = Uri.UnescapeDataString(parts[0]);
                state.Artist = Uri.UnescapeDataString(parts[1]);
                state.ArtworkUrl = Uri.UnescapeDataString(parts[2]);
                state.Paused = parts[3] == "1";
                double.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out state.CurrentTime);
                double.TryParse(parts[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out state.Duration);
                double.TryParse(parts[6], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out state.Volume);
                lastState = state;
                UpdateNowPlaying(state);
                miniPlayer.UpdatePlayer(state);
                // Playback state belongs to the player, never the app notification area.
            }
            catch
            {
            }
            finally { refreshing = false; }
        }

        private void UpdateNowPlaying(PlayerState state)
        {
            nowTitle.Text = string.IsNullOrWhiteSpace(state.Title) ? "Nothing playing" : state.Title;
            nowArtist.Text = string.IsNullOrWhiteSpace(state.Artist) ? "YouTube Music" : state.Artist;
            playPauseButton.AccessibleName = state.Paused ? "Play" : "Pause";
            playPauseButton.Text = state.Paused ? "▶" : "❚❚";
            progress.Ratio = state.Duration > 0 ? state.CurrentTime / state.Duration : 0;
            progress.Interactive = state.Duration > 0;
            volume.Ratio = state.Volume;
            timeLabel.Text = FormatTime(state.CurrentTime) + " / " + FormatTime(state.Duration);

            if (!string.IsNullOrWhiteSpace(state.ArtworkUrl) && !string.Equals(artworkUrl, state.ArtworkUrl, StringComparison.Ordinal))
            {
                artworkUrl = state.ArtworkUrl;
                try { artwork.LoadAsync(artworkUrl); }
                catch { }
            }
        }

        private static string FormatTime(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) value = 0;
            int total = (int)Math.Floor(value);
            int minutes = total / 60;
            int seconds = total % 60;
            return minutes.ToString() + ":" + seconds.ToString("00");
        }

        public async void TogglePlayback()
        {
            await ExecutePlayerScriptAsync(@"(() => { const m = document.querySelector('video, audio'); if (!m) return; if (m.paused) m.play().catch(() => {}); else m.pause(); })();");
        }

        public async void PreviousTrack()
        {
            await ExecutePlayerScriptAsync(@"(() => { const b = document.querySelector('ytmusic-player-bar .previous-button, ytmusic-player-bar #previous-button'); if (b) b.click(); })();");
        }

        public async void NextTrack()
        {
            await ExecutePlayerScriptAsync(@"(() => { const b = document.querySelector('ytmusic-player-bar .next-button, ytmusic-player-bar #next-button'); if (b) b.click(); })();");
        }

        public async void SetVolume(double value)
        {
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            string number = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ExecutePlayerScriptAsync("(() => { const m = document.querySelector('video, audio'); if (m) m.volume = " + number + "; })();");
        }

        public async void SeekToRatio(double ratio)
        {
            if (lastState == null || lastState.Duration <= 0) return;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            double target = lastState.Duration * ratio;
            string number = target.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ExecutePlayerScriptAsync("(() => { const m = document.querySelector('video, audio'); if (m) m.currentTime = " + number + "; })();");
        }

        private async Task ExecutePlayerScriptAsync(string script)
        {
            if (!initialized) return;
            WakeWebView();
            try
            {
                await web.CoreWebView2.ExecuteScriptAsync(script);
                await RefreshPlayerStateAsync();
            }
            catch { }
        }

        public void ShowMiniPlayer()
        {
            WakeWebView();
            miniPlayer.UpdatePlayer(lastState);
            miniPlayer.ShowNearTaskbar();
        }

        public void RestoreMainWindow()
        {
            WakeWebView();
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ShowSettings() { ShowSettings(false); }
        private void ShowSettings(bool updates)
        {
            if (settingsWindow == null || settingsWindow.IsDisposed)
            {
                settingsWindow = new SettingsForm(this);
                settingsWindow.Show(this);
            }
            if (updates) settingsWindow.ShowUpdates();
            settingsWindow.Activate();
        }

        private void HideToTray()
        {
            Hide();
            if (!trayExplained)
            {
                trayExplained = true;
                SaveSettings();
                tray.ShowBalloonTip(6000, "Music stays with you", "YT Music Lite is still running. Double-click its tray icon to reopen; right-click and choose Exit to quit.", ToolTipIcon.Info);
            }
        }

        private async Task HandleWindowStateAsync()
        {
            if (!initialized) return;
            if (WindowState == FormWindowState.Minimized && minimizeToTray)
            {
                HideToTray();
                if (lastState.Paused) await SuspendWebViewAsync(false);
            }
            else if (WindowState != FormWindowState.Minimized && autoSuspended && !manualSleep)
                WakeWebView();
        }

        public async void SleepNow()
        {
            if (!initialized || suspending) return;
            await ExecutePlayerScriptAsync(@"(() => { const m = document.querySelector('video, audio'); if (m) m.pause(); })();");
            await SuspendWebViewAsync(true);
        }

        private async Task SuspendWebViewAsync(bool manual)
        {
            if (!initialized || suspending || web.CoreWebView2 == null) return;
            suspending = true;
            int generation = wakeGeneration;
            try
            {
                web.Visible = false;
                bool ok = await web.CoreWebView2.TrySuspendAsync();
                if (generation != wakeGeneration)
                {
                    web.CoreWebView2.Resume();
                    web.Visible = true;
                    return;
                }
                if (ok)
                {
                    manualSleep = manual;
                    autoSuspended = !manual;
                    if (manual) ShowState("Taking a break", "Music paused to save resources.", "Resume", delegate { WakeWebView(); web.Focus(); });
                }
                else
                {
                    web.Visible = true;
                    if (manual) ShowState("Music is paused", "The app couldn’t enter sleep mode. You can return to your music.", "Return to music", delegate { WakeWebView(); web.Focus(); });
                }
            }
            catch
            {
                web.Visible = true;
                if (manual) ShowState("Music is paused", "Sleep mode is unavailable right now.", "Return to music", delegate { WakeWebView(); web.Focus(); });
            }
            finally { suspending = false; }
        }

        private void WakeWebView()
        {
            if (!initialized) return;
            wakeGeneration++;
            if (web.CoreWebView2 != null) web.CoreWebView2.Resume();
            manualSleep = false;
            autoSuspended = false;
            web.Visible = true;
            HideState();
            status.Text = "";
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (exiting) return;
            if (closeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                if (lastState.Paused) SuspendWebViewAsync(false);
                return;
            }

            exiting = true;
            tray.Visible = false;
            miniPlayer.Close();
        }

        private void NotifyUpdateChanged()
        {
            if (IsDisposed) return;
            updateBadge.Visible = pendingUpdate != null;
            updateBadge.Text = preparedUpdate == null ? "Update" : "Restart";
            if (UpdateChanged != null) UpdateChanged(this, EventArgs.Empty);
        }

        public async void CheckForUpdatesInteractive()
        {
            if (UpdateBusy || UpdateReady) return;
            updateCheckRunning = true;
            UpdateMessage = "Checking for updates…";
            UpdateProgress = 0;
            NotifyUpdateChanged();
            try
            {
                UpdateCheckResult result = await updateService.CheckAsync();
                pendingUpdate = result.UpdateAvailable ? result : null;
                UpdateMessage = result.Message;
                UpdateNotes = result.ReleaseNotes ?? "";
            }
            catch (Exception ex)
            {
                UpdateMessage = "Couldn’t check for updates. Check your connection and try again.";
                UpdateNotes = ex.Message;
            }
            finally { updateCheckRunning = false; NotifyUpdateChanged(); }
        }

        public async void DownloadUpdate()
        {
            if (UpdateBusy || pendingUpdate == null || UpdateReady) return;
            updateDownloading = true;
            UpdateProgress = 0;
            UpdateMessage = "Downloading update… You can keep listening.";
            NotifyUpdateChanged();
            try
            {
                Progress<int> progress = new Progress<int>(delegate(int value)
                {
                    if (!updateDownloading || IsDisposed) return;
                    UpdateProgress = value;
                    UpdateMessage = value >= 100 ? "Verifying download…" : "Downloading update… " + value + "%";
                    NotifyUpdateChanged();
                });
                preparedUpdate = await updateService.PrepareAsync(pendingUpdate, progress);
                UpdateMessage = "Update ready. Restart when you are ready to stop playback.";
            }
            catch (Exception ex)
            {
                UpdateMessage = "Couldn’t prepare the update. Try downloading again.";
                UpdateNotes = ex.Message;
                UpdateProgress = 0;
            }
            finally { updateDownloading = false; NotifyUpdateChanged(); }
        }

        public void RestartForUpdate()
        {
            if (preparedUpdate == null || UpdateBusy) return;
            try
            {
                updateService.InstallPrepared(preparedUpdate);
                ExitApplication();
            }
            catch (Exception ex)
            {
                preparedUpdate = null;
                UpdateMessage = "Couldn’t install the update. Try downloading again.";
                UpdateNotes = ex.Message;
                NotifyUpdateChanged();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                exiting = true;
                if (stateTimer != null) { stateTimer.Stop(); stateTimer.Dispose(); }
                if (tray != null) tray.Dispose();
                if (tips != null) tips.Dispose();
                if (settingsWindow != null) settingsWindow.Dispose();
                if (miniPlayer != null) miniPlayer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ExitApplication()
        {
            exiting = true;
            tray.Visible = false;
            miniPlayer.Close();
            Close();
            Application.Exit();
        }
    }
}
