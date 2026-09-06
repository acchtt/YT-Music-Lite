using System;
using System.Drawing;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class SettingsForm : Form
    {
        private readonly MainForm owner;
        private readonly Panel content;
        private readonly TableLayoutPanel generalPage;
        private readonly TableLayoutPanel aboutPage;
        private readonly LiteButton generalNav;
        private readonly LiteButton aboutNav;
        private Label updateStatus;
        private TextBox releaseNotes;
        private ProgressBar updateProgress;
        private LiteButton checkUpdate;
        private LiteButton downloadUpdate;
        private LiteButton restartUpdate;

        public SettingsForm(MainForm mainForm)
        {
            owner = mainForm;
            AutoScaleDimensions = new SizeF(6f, 13f);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "Settings — YT Music Lite";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ClientSize = new Size(680, 520);
            MinimumSize = new Size(600, 450);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(19, 19, 19);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; } };

            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            FlowLayoutPanel nav = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12, 20, 12, 12), BackColor = Color.FromArgb(13, 13, 13), Margin = Padding.Empty };
            shell.Controls.Add(nav, 0, 0);
            nav.Controls.Add(new Label { Text = "YT Music Lite", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(4, 0, 0, 20) });
            generalNav = MakeButton("General", delegate { ShowPage(false); });
            aboutNav = MakeButton("About & updates", delegate { ShowPage(true); });
            generalNav.Width = aboutNav.Width = 125;
            nav.Controls.Add(generalNav);
            nav.Controls.Add(aboutNav);
            content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24), Margin = Padding.Empty };
            shell.Controls.Add(content, 1, 0);
            generalPage = BuildGeneralPage();
            aboutPage = BuildAboutPage();
            content.Controls.Add(generalPage);
            content.Controls.Add(aboutPage);
            owner.UpdateChanged += OnUpdateChanged;
            ShowPage(false);
            RefreshUpdate();
        }

        private TableLayoutPanel NewPage(string title, string description)
        {
            TableLayoutPanel page = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Margin = Padding.Empty };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Add(page, new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 18f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
            Add(page, new Label { Text = description, AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(175, 175, 175), Margin = new Padding(0, 0, 0, 24) });
            return page;
        }

        private static void Add(TableLayoutPanel page, Control control)
        {
            int row = page.RowCount++;
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.Controls.Add(control, 0, row);
        }

        private TableLayoutPanel BuildGeneralPage()
        {
            TableLayoutPanel page = NewPage("General", "Make the desktop app work your way.");
            AddSetting(page, "Minimize to tray", "Hide the window when minimized.", owner.MinimizeToTrayEnabled, owner.SetMinimizeToTray);
            AddSetting(page, "Close to tray", "Keep music playing when the window closes. Quit from the tray menu.", owner.CloseToTrayEnabled, owner.SetCloseToTray);
            AddSetting(page, "Mini player always on top", "Keep playback controls above other windows.", owner.MiniAlwaysOnTop, owner.SetMiniAlwaysOnTop);
            AddSetting(page, "Automatic update checks", "Check at startup. Download and restart only when you choose.", owner.AutomaticUpdateChecksEnabled, owner.SetAutomaticUpdateChecks);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
            actions.Controls.Add(MakeButton("Open mini player", owner.ShowMiniPlayer));
            actions.Controls.Add(MakeButton("Pause and sleep", delegate { owner.SleepNow(); Close(); }));
            Add(page, actions);
            Add(page, new Label { Text = "Sleep pauses music and reduces resource use. Resume from the main window.", AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(175, 175, 175), Margin = new Padding(0, 12, 0, 0) });
            return page;
        }

        private void AddSetting(TableLayoutPanel page, string title, string description, bool initial, Action<bool> change)
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Padding = new Padding(14), Margin = new Padding(0, 0, 0, 8), BackColor = Color.FromArgb(27, 27, 27) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label name = new Label { Text = title, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 6) };
            Label detail = new Label { Text = description, AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(175, 175, 175), Margin = new Padding(0, 0, 12, 0) };
            ToggleSwitch toggle = new ToggleSwitch { Checked = initial, AccessibleName = title, AccessibleDescription = description, Anchor = AnchorStyles.Right, BackColor = row.BackColor };
            toggle.CheckedChanged += delegate { change(toggle.Checked); };
            name.Cursor = Cursors.Hand;
            name.Click += delegate { toggle.Checked = !toggle.Checked; toggle.Focus(); };
            row.Controls.Add(name, 0, 0);
            row.Controls.Add(detail, 0, 1);
            row.Controls.Add(toggle, 1, 0);
            row.SetRowSpan(toggle, 2);
            Add(page, row);
        }

        private TableLayoutPanel BuildAboutPage()
        {
            TableLayoutPanel page = NewPage("About & updates", "YT Music Lite " + UpdateService.CurrentVersion + " for Windows");
            Add(page, new Label { Text = "Your YouTube Music library, with desktop controls close at hand.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 20) });
            updateStatus = new Label { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12), AccessibleName = "Update status" };
            Add(page, updateStatus);
            updateProgress = new ProgressBar { Dock = DockStyle.Fill, Height = 12, Margin = new Padding(0, 0, 0, 16), AccessibleName = "Update download progress" };
            Add(page, updateProgress);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty };
            checkUpdate = MakeButton("Check for updates", owner.CheckForUpdatesInteractive);
            downloadUpdate = MakeButton("Download update", owner.DownloadUpdate);
            restartUpdate = MakeButton("Restart to update", owner.RestartForUpdate);
            actions.Controls.Add(checkUpdate);
            actions.Controls.Add(downloadUpdate);
            actions.Controls.Add(restartUpdate);
            Add(page, actions);
            releaseNotes = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Height = 170, BackColor = BackColor, ForeColor = Color.FromArgb(185, 185, 185), Margin = new Padding(0, 20, 0, 0), AccessibleName = "Release notes or update details" };
            Add(page, releaseNotes);
            return page;
        }

        private LiteButton MakeButton(string text, Action action)
        {
            LiteButton button = new LiteButton { Text = text, AccessibleName = text, AutoSize = true, Padding = new Padding(8, 6, 8, 6), Margin = new Padding(0, 0, 8, 8), Font = Font };
            button.Click += delegate { action(); };
            return button;
        }

        public void ShowUpdates() { ShowPage(true); }
        private void ShowPage(bool about)
        {
            generalPage.Visible = !about;
            aboutPage.Visible = about;
            (about ? aboutPage : generalPage).BringToFront();
            generalNav.BackColor = about ? Color.FromArgb(20, 20, 20) : Color.FromArgb(65, 27, 33);
            aboutNav.BackColor = about ? Color.FromArgb(65, 27, 33) : Color.FromArgb(20, 20, 20);
            content.AutoScrollPosition = Point.Empty;
        }

        private void OnUpdateChanged(object sender, EventArgs e) { RefreshUpdate(); }
        private void RefreshUpdate()
        {
            updateStatus.Text = owner.UpdateMessage;
            releaseNotes.Text = owner.UpdateNotes;
            releaseNotes.Visible = !string.IsNullOrWhiteSpace(owner.UpdateNotes);
            updateProgress.Visible = owner.UpdateBusy;
            updateProgress.Style = owner.UpdateBusy && owner.UpdateProgress == 0 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            updateProgress.Value = Math.Max(0, Math.Min(100, owner.UpdateProgress));
            checkUpdate.Enabled = !owner.UpdateBusy && !owner.UpdateReady;
            downloadUpdate.Visible = owner.UpdateAvailable && !owner.UpdateReady;
            downloadUpdate.Enabled = !owner.UpdateBusy;
            restartUpdate.Visible = owner.UpdateReady;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) owner.UpdateChanged -= OnUpdateChanged;
            base.Dispose(disposing);
        }
    }
}
