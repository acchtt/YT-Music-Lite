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
        private readonly Label updateBadge;
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

        public MainForm()
        {
            Text = "YT Music Lite";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            Size = new Size(1280, 820);
            BackColor = Color.FromArgb(10, 10, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Icon = SystemIcons.Application;

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

            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 54;
            topBar.BackColor = Color.FromArgb(18, 18, 18);

            Label brandMark = new Label();
            brandMark.Text = "●";
            brandMark.ForeColor = Color.FromArgb(255, 50, 72);
            brandMark.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            brandMark.Location = new Point(15, 13);
            brandMark.AutoSize = true;
            topBar.Controls.Add(brandMark);

            Label brand = new Label();
            brand.Text = "YT Music Lite";
            brand.ForeColor = Color.White;
            brand.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            brand.Location = new Point(40, 16);
            brand.Size = new Size(116, 25);
            topBar.Controls.Add(brand);

            int navX = 165;
            LiteButton back = MakeTopButton("‹", navX, "Back");
            back.Click += delegate { if (initialized && web.CanGoBack) web.GoBack(); };
            navX += 38;
            LiteButton forward = MakeTopButton("›", navX, "Forward");
            forward.Click += delegate { if (initialized && web.CanGoForward) web.GoForward(); };
            navX += 38;
            LiteButton reload = MakeTopButton("↻", navX, "Reload");
            reload.Click += delegate { if (initialized) web.Reload(); };
            navX += 38;
            LiteButton home = MakeTopButton("⌂", navX, "YouTube Music home");
            home.Click += delegate { NavigateHome(); };

            status = new Label();
            status.Text = "Starting…";
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

            updateBadge = new Label();
            updateBadge.Text = "UPDATE";
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
            updateBadge.Click += delegate { CheckForUpdatesInteractive(); };
            tips.SetToolTip(updateBadge, "A new YT Music Lite version is available");
            topBar.Controls.Add(updateBadge);

            web = new WebView2();
            web.Dock = DockStyle.Fill;
            web.BackColor = Color.Black;

            Panel webHost = new Panel();
            webHost.Dock = DockStyle.Fill;
            webHost.BackColor = Color.Black;
            webHost.Padding = new Padding(0, 1, 0, 0);
            webHost.Controls.Add(web);

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
            menu.Items.Add("Sleep", null, delegate { SleepNow(); });
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
            tray.Icon = SystemIcons.Application;
            tray.Text = "YT Music Lite";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { RestoreMainWindow(); };

            playerBar.Resize += delegate { LayoutPlayerControls(); };
            Resize += async delegate
            {
                LayoutTopControls(settingsButton, miniButton);
                await HandleWindowStateAsync();
            };
            Load += async delegate
            {
                LayoutPlayerControls();
                LayoutTopControls(settingsButton, miniButton);
                await InitializeWebViewAsync();
                if (automaticUpdateChecks) CheckForUpdates(false);
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
            tips.SetToolTip(button, tip);
            return button;
        }

        private LiteButton MakePlayerButton(string text, string tip)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.Font = new Font("Segoe UI Symbol", 12f, FontStyle.Regular);
            button.Size = new Size(40, 38);
            tips.SetToolTip(button, tip);
            return button;
        }

        private void LayoutTopControls(Control settingsButton, Control miniButton)
        {
            if (settingsButton == null || miniButton == null) return;
            settingsButton.Location = new Point(Math.Max(0, topBar.ClientSize.Width - 48), 9);
            miniButton.Location = new Point(Math.Max(0, topBar.ClientSize.Width - 90), 9);
            updateBadge.Location = new Point(Math.Max(0, topBar.ClientSize.Width - 164), 15);
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
            try
            {
                if (!File.Exists(settingsPath)) return;
                string[] lines = File.ReadAllLines(settingsPath);
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
                }
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
                    "automatic_update_checks=" + (automaticUpdateChecks ? "1" : "0") + Environment.NewLine;
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

        private async Task InitializeWebViewAsync()
        {
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
                web.CoreWebView2.NavigationStarting += delegate { status.Text = "Loading…"; };
                web.CoreWebView2.NavigationCompleted += async delegate(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    status.Text = e.IsSuccess ? "Ready" : "Navigation error";
                    if (e.IsSuccess) await InjectLiteModeAsync();
                };

                initialized = true;
                stateTimer.Start();
                NavigateHome();
            }
            catch (Exception ex)
            {
                status.Text = "WebView2 failed";
                MessageBox.Show(
                    "YT Music Lite could not start WebView2.\r\n\r\n" + ex.Message +
                    "\r\n\r\nRun install-webview2-runtime.cmd, then launch again.",
                    "YT Music Lite",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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
            if (!initialized || autoSuspended || manualSleep || web.CoreWebView2 == null) return;
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
                status.Text = state.Paused ? "Paused" : "Playing";
            }
            catch
            {
            }
        }

        private void UpdateNowPlaying(PlayerState state)
        {
            nowTitle.Text = string.IsNullOrWhiteSpace(state.Title) ? "Nothing playing" : state.Title;
            nowArtist.Text = string.IsNullOrWhiteSpace(state.Artist) ? "YouTube Music" : state.Artist;
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

        private void ShowSettings()
        {
            using (SettingsForm settings = new SettingsForm(this))
            {
                settings.ShowDialog(this);
            }
        }

        private async Task HandleWindowStateAsync()
        {
            if (!initialized) return;
            if (WindowState == FormWindowState.Minimized && minimizeToTray)
            {
                Hide();
                if (lastState.Paused) await SuspendWebViewAsync(false);
            }
            else if (WindowState != FormWindowState.Minimized)
            {
                WakeWebView();
            }
        }

        public async void SleepNow()
        {
            if (!initialized) return;
            await ExecutePlayerScriptAsync(@"(() => { const m = document.querySelector('video, audio'); if (m) m.pause(); })();");
            await SuspendWebViewAsync(true);
            status.Text = "Sleeping";
        }

        private async Task SuspendWebViewAsync(bool manual)
        {
            if (!initialized || web.CoreWebView2 == null) return;
            try
            {
                web.Visible = false;
                bool ok = await web.CoreWebView2.TrySuspendAsync();
                if (ok)
                {
                    manualSleep = manual;
                    autoSuspended = !manual;
                }
                else web.Visible = Visible;
            }
            catch { web.Visible = Visible; }
        }

        private void WakeWebView()
        {
            if (!initialized) return;
            manualSleep = false;
            autoSuspended = false;
            web.Visible = true;
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (exiting) return;
            if (closeToTray)
            {
                e.Cancel = true;
                Hide();
                if (lastState.Paused) SuspendWebViewAsync(false);
                return;
            }

            exiting = true;
            tray.Visible = false;
            miniPlayer.Close();
        }

        public void CheckForUpdatesInteractive()
        {
            CheckForUpdates(true);
        }

        private async void CheckForUpdates(bool interactive)
        {
            if (updateCheckRunning) return;
            updateCheckRunning = true;
            string previousStatus = status.Text;
            if (interactive) status.Text = "Checking updates…";

            try
            {
                UpdateCheckResult result = await updateService.CheckAsync();
                if (result.UpdateAvailable)
                {
                    pendingUpdate = result;
                    updateBadge.Visible = true;
                    updateBadge.Text = "UPDATE";
                    if (interactive)
                    {
                        DialogResult choice = MessageBox.Show(
                            result.Message + "\r\n\r\nDownload, verify, install, and restart now?",
                            "YT Music Lite Update",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);
                        if (choice == DialogResult.Yes) await InstallUpdateAsync(result);
                    }
                    else status.Text = previousStatus;
                }
                else
                {
                    pendingUpdate = null;
                    updateBadge.Visible = false;
                    if (interactive)
                    {
                        MessageBox.Show(result.Message, "YT Music Lite Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        status.Text = "Up to date";
                    }
                    else status.Text = previousStatus;
                }
            }
            catch (WebException ex)
            {
                status.Text = previousStatus;
                if (interactive)
                {
                    string message = ex.Message;
                    if (ex.Response is HttpWebResponse && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                        message = "The YT Music Lite release channel has not been published yet.";
                    MessageBox.Show(message, "YT Music Lite Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                status.Text = previousStatus;
                if (interactive) MessageBox.Show(ex.Message, "YT Music Lite Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                updateCheckRunning = false;
            }
        }

        private async Task InstallUpdateAsync(UpdateCheckResult result)
        {
            status.Text = "Downloading update…";
            await updateService.DownloadAndInstallAsync(result, this);
            exiting = true;
            tray.Visible = false;
            miniPlayer.Close();
            Close();
            Application.Exit();
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
