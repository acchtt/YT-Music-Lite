using System;
using System.Drawing;
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

        public SettingsForm(MainForm mainForm)
        {
            owner = mainForm;
            Text = "Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(590, 430);
            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 146;
            sidebar.BackColor = Color.FromArgb(14, 14, 14);
            Controls.Add(sidebar);

            Label appName = new Label();
            appName.Text = "YT Music Lite";
            appName.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            appName.ForeColor = Color.FromArgb(238, 238, 238);
            appName.Location = new Point(18, 18);
            appName.Size = new Size(110, 22);
            sidebar.Controls.Add(appName);

            generalNav = MakeNavButton("General", 60);
            generalNav.Click += delegate { ShowPage(false); };
            sidebar.Controls.Add(generalNav);

            aboutNav = MakeNavButton("About", 98);
            aboutNav.Click += delegate { ShowPage(true); };
            sidebar.Controls.Add(aboutNav);

            Label version = new Label();
            version.Text = "v" + UpdateService.CurrentVersion;
            version.ForeColor = Color.FromArgb(92, 92, 92);
            version.Location = new Point(18, 392);
            version.Size = new Size(90, 18);
            sidebar.Controls.Add(version);

            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Color.FromArgb(20, 20, 20);
            Controls.Add(content);
            sidebar.BringToFront();

            generalPage = BuildGeneralPage();
            aboutPage = BuildAboutPage();
            content.Controls.Add(generalPage);
            content.Controls.Add(aboutPage);

            ShowPage(false);
        }

        private Panel BuildGeneralPage()
        {
            Panel page = NewPage();

            Label title = MakeTitle("General", 28);
            page.Controls.Add(title);

            Label hint = MakeMuted("Window behavior and background activity", 59, 320);
            page.Controls.Add(hint);

            int y = 105;
            y = AddSettingRow(
                page,
                "Minimize to tray",
                "Hide the main window when you minimize it.",
                y,
                owner.MinimizeToTrayEnabled,
                delegate(bool value) { owner.SetMinimizeToTray(value); });

            y = AddSettingRow(
                page,
                "Close to tray",
                "Keep music running when the close button is pressed.",
                y,
                owner.CloseToTrayEnabled,
                delegate(bool value) { owner.SetCloseToTray(value); });

            y = AddSettingRow(
                page,
                "Automatic updates",
                "Check for new releases quietly when the app starts.",
                y,
                owner.AutomaticUpdateChecksEnabled,
                delegate(bool value) { owner.SetAutomaticUpdateChecks(value); });

            Label tools = MakeMuted("Actions", y + 18, 120);
            tools.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            tools.ForeColor = Color.FromArgb(145, 145, 145);
            page.Controls.Add(tools);

            LiteButton mini = MakeQuietButton("Mini player", 28, y + 48, 102);
            mini.Click += delegate { owner.ShowMiniPlayer(); };
            page.Controls.Add(mini);

            LiteButton sleep = MakeQuietButton("Sleep", 138, y + 48, 78);
            sleep.Click += delegate { owner.SleepNow(); };
            page.Controls.Add(sleep);

            LiteButton update = MakeQuietButton("Check for updates", 224, y + 48, 132);
            update.Click += delegate { owner.CheckForUpdatesInteractive(); };
            page.Controls.Add(update);

            return page;
        }

        private Panel BuildAboutPage()
        {
            Panel page = NewPage();

            Label title = MakeTitle("About", 28);
            page.Controls.Add(title);

            Label product = new Label();
            product.Text = "YT Music Lite";
            product.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            product.ForeColor = Color.White;
            product.Location = new Point(28, 92);
            product.Size = new Size(220, 28);
            page.Controls.Add(product);

            Label version = MakeMuted("Version " + UpdateService.CurrentVersion, 124, 220);
            page.Controls.Add(version);

            Label description = new Label();
            description.Text = "A lightweight Windows shell for YouTube Music, built around Microsoft WebView2.";
            description.ForeColor = Color.FromArgb(175, 175, 175);
            description.Location = new Point(28, 168);
            description.Size = new Size(365, 48);
            page.Controls.Add(description);

            AddInfoLine(page, "Engine", "Microsoft WebView2", 242);
            AddInfoLine(page, "Runtime", ".NET Framework", 274);
            AddInfoLine(page, "Updates", "GitHub Releases", 306);

            LiteButton check = MakeQuietButton("Check for updates", 28, 354, 132);
            check.Click += delegate { owner.CheckForUpdatesInteractive(); };
            page.Controls.Add(check);

            return page;
        }

        private Panel NewPage()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = Color.FromArgb(20, 20, 20);
            return page;
        }

        private int AddSettingRow(
            Panel page,
            string title,
            string description,
            int y,
            bool initial,
            Action<bool> changed)
        {
            Panel row = new Panel();
            row.Location = new Point(28, y);
            row.Size = new Size(385, 66);
            row.BackColor = page.BackColor;
            page.Controls.Add(row);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            titleLabel.ForeColor = Color.FromArgb(238, 238, 238);
            titleLabel.Location = new Point(0, 4);
            titleLabel.Size = new Size(270, 22);
            row.Controls.Add(titleLabel);

            Label descriptionLabel = new Label();
            descriptionLabel.Text = description;
            descriptionLabel.Font = new Font("Segoe UI", 8.7f, FontStyle.Regular);
            descriptionLabel.ForeColor = Color.FromArgb(125, 125, 125);
            descriptionLabel.Location = new Point(0, 28);
            descriptionLabel.Size = new Size(300, 24);
            row.Controls.Add(descriptionLabel);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = initial;
            toggle.Location = new Point(333, 14);
            toggle.CheckedChanged += delegate { changed(toggle.Checked); };
            row.Controls.Add(toggle);

            Panel separator = new Panel();
            separator.Location = new Point(0, 64);
            separator.Size = new Size(385, 1);
            separator.BackColor = Color.FromArgb(39, 39, 39);
            row.Controls.Add(separator);

            return y + 67;
        }

        private LiteButton MakeNavButton(string text, int y)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            button.Location = new Point(10, y);
            button.Size = new Size(126, 34);
            button.Padding = new Padding(8, 0, 0, 0);
            button.BackColor = Color.FromArgb(14, 14, 14);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(36, 36, 36);
            return button;
        }

        private LiteButton MakeQuietButton(string text, int x, int y, int width)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 34);
            button.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            button.BackColor = Color.FromArgb(31, 31, 31);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(43, 43, 43);
            return button;
        }

        private Label MakeTitle(string text, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            label.ForeColor = Color.White;
            label.Location = new Point(28, y);
            label.Size = new Size(300, 32);
            return label;
        }

        private Label MakeMuted(string text, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(125, 125, 125);
            label.Location = new Point(28, y);
            label.Size = new Size(width, 22);
            return label;
        }

        private void AddInfoLine(Panel page, string key, string value, int y)
        {
            Label left = MakeMuted(key, y, 95);
            page.Controls.Add(left);

            Label right = new Label();
            right.Text = value;
            right.ForeColor = Color.FromArgb(220, 220, 220);
            right.Location = new Point(138, y);
            right.Size = new Size(240, 22);
            page.Controls.Add(right);
        }

        private void ShowPage(bool about)
        {
            aboutPage.Visible = about;
            generalPage.Visible = !about;
            if (about) aboutPage.BringToFront(); else generalPage.BringToFront();

            generalNav.BackColor = about ? Color.FromArgb(14, 14, 14) : Color.FromArgb(32, 32, 32);
            aboutNav.BackColor = about ? Color.FromArgb(32, 32, 32) : Color.FromArgb(14, 14, 14);
            generalNav.ForeColor = about ? Color.FromArgb(165, 165, 165) : Color.White;
            aboutNav.ForeColor = about ? Color.White : Color.FromArgb(165, 165, 165);
        }
    }
}
