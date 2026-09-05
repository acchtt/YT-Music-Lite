using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class SettingsForm : Form
    {
        private readonly MainForm owner;
        private readonly Panel content;
        private readonly Panel generalPage;
        private readonly Panel aboutPage;
        private readonly LiteButton generalNav;
        private readonly LiteButton aboutNav;
        private readonly Panel generalIndicator;
        private readonly Panel aboutIndicator;

        private bool dragging;
        private Point dragOrigin;
        private Point formOrigin;

        public SettingsForm(MainForm mainForm)
        {
            owner = mainForm;
            Text = "Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(590, 430);
            BackColor = Color.FromArgb(17, 17, 17);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            DoubleBuffered = true;

            Panel header = new Panel();
            header.Location = new Point(1, 1);
            header.Size = new Size(588, 38);
            header.BackColor = Color.FromArgb(17, 17, 17);
            header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.MouseDown += HeaderMouseDown;
            header.MouseMove += HeaderMouseMove;
            header.MouseUp += HeaderMouseUp;
            Controls.Add(header);

            Label windowTitle = new Label();
            windowTitle.Text = "Settings";
            windowTitle.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            windowTitle.ForeColor = Color.FromArgb(185, 185, 185);
            windowTitle.Location = new Point(14, 10);
            windowTitle.Size = new Size(120, 20);
            windowTitle.MouseDown += HeaderMouseDown;
            windowTitle.MouseMove += HeaderMouseMove;
            windowTitle.MouseUp += HeaderMouseUp;
            header.Controls.Add(windowTitle);

            IconButton close = new IconButton();
            close.Icon = IconKind.Close;
            close.ButtonStyle = IconButtonStyle.Danger;
            close.ForeColor = Color.FromArgb(188, 188, 188);
            close.Size = new Size(34, 30);
            close.Location = new Point(550, 4);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            header.Controls.Add(close);

            Panel headerLine = new Panel();
            headerLine.Location = new Point(0, 37);
            headerLine.Size = new Size(588, 1);
            headerLine.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            headerLine.BackColor = Color.FromArgb(37, 37, 37);
            header.Controls.Add(headerLine);

            Panel sidebar = new Panel();
            sidebar.Location = new Point(1, 39);
            sidebar.Size = new Size(136, 390);
            sidebar.BackColor = Color.FromArgb(13, 13, 13);
            sidebar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(sidebar);

            Label mark = new Label();
            mark.Text = "●";
            mark.ForeColor = Color.FromArgb(238, 47, 67);
            mark.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            mark.Location = new Point(17, 18);
            mark.Size = new Size(16, 18);
            sidebar.Controls.Add(mark);

            Label appName = new Label();
            appName.Text = "YT Music Lite";
            appName.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
            appName.ForeColor = Color.FromArgb(235, 235, 235);
            appName.Location = new Point(33, 18);
            appName.Size = new Size(94, 20);
            sidebar.Controls.Add(appName);

            generalIndicator = MakeIndicator(58);
            sidebar.Controls.Add(generalIndicator);
            generalNav = MakeNavButton("General", 52);
            generalNav.Click += delegate { ShowPage(false); };
            sidebar.Controls.Add(generalNav);

            aboutIndicator = MakeIndicator(96);
            sidebar.Controls.Add(aboutIndicator);
            aboutNav = MakeNavButton("About", 90);
            aboutNav.Click += delegate { ShowPage(true); };
            sidebar.Controls.Add(aboutNav);

            Label version = new Label();
            version.Text = "v" + UpdateService.CurrentVersion;
            version.ForeColor = Color.FromArgb(82, 82, 82);
            version.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            version.Location = new Point(17, 360);
            version.Size = new Size(96, 18);
            version.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            sidebar.Controls.Add(version);

            Panel splitLine = new Panel();
            splitLine.Location = new Point(135, 0);
            splitLine.Size = new Size(1, 390);
            splitLine.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            splitLine.BackColor = Color.FromArgb(34, 34, 34);
            sidebar.Controls.Add(splitLine);

            // Explicit split bounds avoid WinForms Dock=Fill extending behind the sidebar.
            content = new Panel();
            content.Location = new Point(137, 39);
            content.Size = new Size(452, 390);
            content.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            content.BackColor = Color.FromArgb(19, 19, 19);
            Controls.Add(content);

            generalPage = BuildGeneralPage();
            aboutPage = BuildAboutPage();
            content.Controls.Add(generalPage);
            content.Controls.Add(aboutPage);
            ShowPage(false);

            Shown += delegate { ApplyRoundedRegion(); };
            Resize += delegate { ApplyRoundedRegion(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Color.FromArgb(58, 58, 58)))
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10)) e.Graphics.DrawPath(pen, path);
        }

        private Panel BuildGeneralPage()
        {
            Panel page = NewPage();
            page.Controls.Add(MakeTitle("General", 20));
            page.Controls.Add(MakeMuted("Window and background behavior", 49, 310));

            Panel settingsSurface = new Panel();
            settingsSurface.Location = new Point(26, 82);
            settingsSurface.Size = new Size(400, 190);
            settingsSurface.BackColor = Color.FromArgb(22, 22, 22);
            page.Controls.Add(settingsSurface);

            AddSettingRow(settingsSurface, "Minimize to tray", "Hide the window when minimized.", 0, owner.MinimizeToTrayEnabled, delegate(bool value) { owner.SetMinimizeToTray(value); });
            AddSettingRow(settingsSurface, "Close to tray", "Keep music running after closing the window.", 63, owner.CloseToTrayEnabled, delegate(bool value) { owner.SetCloseToTray(value); });
            AddSettingRow(settingsSurface, "Automatic updates", "Check quietly when YT Music Lite starts.", 126, owner.AutomaticUpdateChecksEnabled, delegate(bool value) { owner.SetAutomaticUpdateChecks(value); });

            Label tools = MakeMuted("Quick actions", 294, 120);
            tools.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            tools.ForeColor = Color.FromArgb(132, 132, 132);
            page.Controls.Add(tools);

            IconTextButton mini = MakeActionButton("Mini player", IconKind.MiniPlayer, 26, 323, 110);
            mini.Click += delegate { owner.ShowMiniPlayer(); };
            page.Controls.Add(mini);

            IconTextButton sleep = MakeActionButton("Sleep", IconKind.Sleep, 144, 323, 86);
            sleep.Click += delegate { owner.SleepNow(); };
            page.Controls.Add(sleep);

            IconTextButton update = MakeActionButton("Check updates", IconKind.Update, 238, 323, 128);
            update.Click += delegate { owner.CheckForUpdatesInteractive(); };
            page.Controls.Add(update);

            return page;
        }

        private Panel BuildAboutPage()
        {
            Panel page = NewPage();
            page.Controls.Add(MakeTitle("About", 20));
            page.Controls.Add(MakeMuted("YT Music Lite for Windows", 49, 300));

            Label product = new Label();
            product.Text = "YT Music Lite";
            product.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            product.ForeColor = Color.FromArgb(242, 242, 242);
            product.Location = new Point(26, 92);
            product.Size = new Size(220, 28);
            page.Controls.Add(product);

            page.Controls.Add(MakeMuted("Version " + UpdateService.CurrentVersion, 124, 220));

            Label description = new Label();
            description.Text = "A lightweight Windows shell that leaves YouTube Music responsible for playback while the native app handles desktop controls.";
            description.ForeColor = Color.FromArgb(157, 157, 157);
            description.Location = new Point(26, 164);
            description.Size = new Size(380, 54);
            page.Controls.Add(description);

            Panel info = new Panel();
            info.Location = new Point(26, 235);
            info.Size = new Size(400, 92);
            info.BackColor = Color.FromArgb(22, 22, 22);
            page.Controls.Add(info);
            AddInfoLine(info, "Engine", "Microsoft WebView2", 12);
            AddInfoLine(info, "Runtime", ".NET Framework", 38);
            AddInfoLine(info, "Updates", "GitHub Releases", 64);

            IconTextButton check = MakeActionButton("Check updates", IconKind.Update, 26, 343, 128);
            check.Click += delegate { owner.CheckForUpdatesInteractive(); };
            page.Controls.Add(check);
            return page;
        }

        private Panel NewPage()
        {
            Panel page = new Panel();
            page.Location = new Point(0, 0);
            page.Size = new Size(452, 390);
            page.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            page.BackColor = Color.FromArgb(19, 19, 19);
            return page;
        }

        private void AddSettingRow(Panel surface, string title, string description, int y, bool initial, Action<bool> changed)
        {
            Panel row = new Panel();
            row.Location = new Point(0, y);
            row.Size = new Size(400, 63);
            row.BackColor = surface.BackColor;
            surface.Controls.Add(row);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            titleLabel.ForeColor = Color.FromArgb(230, 230, 230);
            titleLabel.Location = new Point(14, 10);
            titleLabel.Size = new Size(285, 20);
            row.Controls.Add(titleLabel);

            Label descriptionLabel = new Label();
            descriptionLabel.Text = description;
            descriptionLabel.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            descriptionLabel.ForeColor = Color.FromArgb(118, 118, 118);
            descriptionLabel.Location = new Point(14, 33);
            descriptionLabel.Size = new Size(305, 18);
            row.Controls.Add(descriptionLabel);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = initial;
            toggle.Location = new Point(344, 20);
            toggle.CheckedChanged += delegate { changed(toggle.Checked); };
            row.Controls.Add(toggle);

            if (y < 126)
            {
                Panel separator = new Panel();
                separator.Location = new Point(14, 62);
                separator.Size = new Size(372, 1);
                separator.BackColor = Color.FromArgb(38, 38, 38);
                row.Controls.Add(separator);
            }
        }

        private Panel MakeIndicator(int y)
        {
            Panel indicator = new Panel();
            indicator.Location = new Point(0, y);
            indicator.Size = new Size(3, 22);
            indicator.BackColor = Color.FromArgb(238, 47, 67);
            return indicator;
        }

        private LiteButton MakeNavButton(string text, int y)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            button.Location = new Point(8, y);
            button.Size = new Size(120, 34);
            button.Padding = new Padding(10, 0, 0, 0);
            button.BackColor = Color.FromArgb(13, 13, 13);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 25, 25);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(29, 29, 29);
            return button;
        }

        private IconTextButton MakeActionButton(string text, IconKind icon, int x, int y, int width)
        {
            IconTextButton button = new IconTextButton();
            button.Text = text;
            button.Icon = icon;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 32);
            return button;
        }

        private Label MakeTitle(string text, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(242, 242, 242);
            label.Location = new Point(26, y);
            label.Size = new Size(300, 29);
            return label;
        }

        private Label MakeMuted(string text, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(112, 112, 112);
            label.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            label.Location = new Point(26, y);
            label.Size = new Size(width, 20);
            return label;
        }

        private void AddInfoLine(Panel page, string key, string value, int y)
        {
            Label left = new Label();
            left.Text = key;
            left.ForeColor = Color.FromArgb(108, 108, 108);
            left.Location = new Point(14, y);
            left.Size = new Size(92, 20);
            page.Controls.Add(left);

            Label right = new Label();
            right.Text = value;
            right.ForeColor = Color.FromArgb(214, 214, 214);
            right.Location = new Point(116, y);
            right.Size = new Size(250, 20);
            page.Controls.Add(right);
        }

        private void ShowPage(bool about)
        {
            aboutPage.Visible = about;
            generalPage.Visible = !about;
            if (about) aboutPage.BringToFront(); else generalPage.BringToFront();

            generalIndicator.Visible = !about;
            aboutIndicator.Visible = about;
            generalNav.BackColor = Color.FromArgb(13, 13, 13);
            aboutNav.BackColor = Color.FromArgb(13, 13, 13);
            generalNav.ForeColor = about ? Color.FromArgb(145, 145, 145) : Color.White;
            aboutNav.ForeColor = about ? Color.White : Color.FromArgb(145, 145, 145);
        }

        private void HeaderMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragOrigin = Cursor.Position;
            formOrigin = Location;
        }

        private void HeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Point current = Cursor.Position;
            Location = new Point(formOrigin.X + current.X - dragOrigin.X, formOrigin.Y + current.Y - dragOrigin.Y);
        }

        private void HeaderMouseUp(object sender, MouseEventArgs e) { dragging = false; }

        private void ApplyRoundedRegion()
        {
            if (Width <= 2 || Height <= 2) return;
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 10)) Region = new Region(path);
            Invalidate();
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
