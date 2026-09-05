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
        private readonly Panel toolbar;
        private readonly Label status;
        private readonly Timer stateTimer;
        private readonly NotifyIcon tray;
        private readonly MiniPlayerForm miniPlayer;
        private readonly JavaScriptSerializer json;
        private PlayerState lastState;
        private bool initialized;
        private bool exiting;
        private bool autoSuspended;
        private bool manualSleep;
        private readonly UpdateService updateService;
        private bool updateCheckRunning;

        public bool IsExiting { get { return exiting; } }

        public MainForm()
        {
            Text = "YT Music Lite v4";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);
            Size = new Size(1280, 820);
            BackColor = Color.FromArgb(15, 15, 15);
            Icon = SystemIcons.Application;

            json = new JavaScriptSerializer();
            lastState = new PlayerState();
            updateService = new UpdateService();

            toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 40;
            toolbar.BackColor = Color.FromArgb(24, 24, 24);

            int x = 7;
            x = AddToolbarButton("←", x, delegate { if (web.CanGoBack) web.GoBack(); });
            x = AddToolbarButton("→", x, delegate { if (web.CanGoForward) web.GoForward(); });
            x = AddToolbarButton("↻", x, delegate { if (initialized) web.Reload(); });
            x = AddToolbarButton("Home", x, delegate { NavigateHome(); });
            x = AddToolbarButton("Mini", x, delegate { ShowMiniPlayer(); });
            x = AddToolbarButton("Sleep", x, delegate { ManualSleep(); });
            x = AddToolbarButton("Update", x, delegate { CheckForUpdates(true); });

            status = new Label();
            status.Text = "Starting WebView2...";
            status.AutoSize = false;
            status.TextAlign = ContentAlignment.MiddleRight;
            status.ForeColor = Color.Gray;
            status.Dock = DockStyle.Right;
            status.Width = 330;
            status.Padding = new Padding(0, 0, 12, 0);
            toolbar.Controls.Add(status);

            web = new WebView2();
            web.Dock = DockStyle.Fill;
            web.BackColor = Color.Black;

            // Keep the WebView in its own host panel. This makes the 40px
            // toolbar consume layout space instead of floating over YT Music.
            Panel webHost = new Panel();
            webHost.Dock = DockStyle.Fill;
            webHost.BackColor = Color.Black;
            webHost.Controls.Add(web);

            Controls.Add(webHost);
            Controls.Add(toolbar);

            miniPlayer = new MiniPlayerForm(this);

            stateTimer = new Timer();
            stateTimer.Interval = 850;
            stateTimer.Tick += async delegate { await RefreshPlayerStateAsync(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Show YT Music Lite", null, delegate { RestoreMainWindow(); });
            menu.Items.Add("Mini Player", null, delegate { ShowMiniPlayer(); });
            menu.Items.Add("Play / Pause", null, delegate { TogglePlayback(); });
            menu.Items.Add("Sleep", null, delegate { ManualSleep(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitApplication(); });

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "YT Music Lite";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { RestoreMainWindow(); };

            Load += async delegate
            {
                await InitializeWebViewAsync();
                CheckForUpdates(false);
            };
            Resize += async delegate { await HandleWindowStateAsync(); };
            FormClosing += MainFormClosing;
        }

        private int AddToolbarButton(string text, int x, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, 5);
            button.Height = 30;
            button.Width = text.Length > 2 ? 58 : 38;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            button.BackColor = Color.FromArgb(36, 36, 36);
            button.ForeColor = Color.White;
            button.TabStop = false;
            button.Click += handler;
            toolbar.Controls.Add(button);
            return x + button.Width + 5;
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

                web.CoreWebView2.NavigationStarting += delegate
                {
                    status.Text = "Loading...";
                };

                web.CoreWebView2.NavigationCompleted += async delegate(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    status.Text = e.IsSuccess ? "Ready" : "Navigation error";
                    if (e.IsSuccess)
                    {
                        await InjectLiteModeAsync();
                    }
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
            if (initialized)
            {
                WakeWebView();
                web.CoreWebView2.Navigate("https://music.youtube.com/");
            }
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
            try
            {
                await web.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
            }
        }

        private async Task RefreshPlayerStateAsync()
        {
            if (!initialized || autoSuspended || manualSleep) return;
            if (web.CoreWebView2 == null) return;

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
                miniPlayer.UpdatePlayer(state);
                status.Text = state.Paused ? "Paused" : "Playing";
            }
            catch
            {
            }
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

        private async Task ExecutePlayerScriptAsync(string script)
        {
            if (!initialized) return;
            WakeWebView();
            try
            {
                await web.CoreWebView2.ExecuteScriptAsync(script);
                await RefreshPlayerStateAsync();
            }
            catch
            {
            }
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

        private async Task HandleWindowStateAsync()
        {
            if (!initialized) return;

            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                ShowMiniPlayer();
                if (lastState.Paused)
                {
                    await SuspendWebViewAsync(false);
                }
            }
            else
            {
                WakeWebView();
            }
        }

        private async void ManualSleep()
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
                else
                {
                    web.Visible = Visible;
                }
            }
            catch
            {
                web.Visible = Visible;
            }
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
            if (!exiting)
            {
                e.Cancel = true;
                Hide();
                ShowMiniPlayer();
                if (lastState.Paused)
                {
                    SuspendWebViewAsync(false);
                }
            }
        }

        private async void CheckForUpdates(bool interactive)
        {
            if (updateCheckRunning) return;
            updateCheckRunning = true;
            string previousStatus = status.Text;
            if (interactive) status.Text = "Checking for updates...";

            try
            {
                UpdateCheckResult result = await updateService.CheckAsync();
                if (result.UpdateAvailable)
                {
                    status.Text = "Update " + result.Version + " available";
                    DialogResult choice = MessageBox.Show(
                        result.Message + "\r\n\r\nDownload, verify, install, and restart now?",
                        "YT Music Lite Update",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (choice == DialogResult.Yes)
                    {
                        status.Text = "Downloading update...";
                        await updateService.DownloadAndInstallAsync(result, this);
                        exiting = true;
                        tray.Visible = false;
                        miniPlayer.Close();
                        Close();
                        Application.Exit();
                        return;
                    }
                }
                else if (interactive)
                {
                    MessageBox.Show(result.Message, "YT Music Lite Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    status.Text = "Up to date";
                }
                else
                {
                    status.Text = previousStatus;
                }
            }
            catch (WebException ex)
            {
                status.Text = previousStatus;
                if (interactive)
                {
                    string message = ex.Message;
                    if (ex.Response is HttpWebResponse && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        message = "The YT Music Lite release channel has not been published yet.";
                    }
                    MessageBox.Show(message, "YT Music Lite Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                status.Text = previousStatus;
                if (interactive)
                {
                    MessageBox.Show(ex.Message, "YT Music Lite Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                updateCheckRunning = false;
            }
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
