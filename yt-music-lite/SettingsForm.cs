using System;
using System.Drawing;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class SettingsForm : Form
    {
        private readonly MainForm owner;
        private readonly CheckBox minimizeToTray;
        private readonly CheckBox closeToTray;
        private readonly CheckBox automaticUpdates;

        public SettingsForm(MainForm mainForm)
        {
            owner = mainForm;
            Text = "YT Music Lite Settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(470, 420);
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            Label heading = new Label();
            heading.Text = "Settings";
            heading.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            heading.Location = new Point(24, 20);
            heading.AutoSize = true;
            Controls.Add(heading);

            Label behavior = SectionLabel("WINDOW BEHAVIOR", 75);
            Controls.Add(behavior);

            minimizeToTray = MakeCheckBox("Minimize to tray", 106, owner.MinimizeToTrayEnabled);
            minimizeToTray.CheckedChanged += delegate { owner.SetMinimizeToTray(minimizeToTray.Checked); };
            Controls.Add(minimizeToTray);

            closeToTray = MakeCheckBox("Close button sends app to tray", 140, owner.CloseToTrayEnabled);
            closeToTray.CheckedChanged += delegate { owner.SetCloseToTray(closeToTray.Checked); };
            Controls.Add(closeToTray);

            automaticUpdates = MakeCheckBox("Check for updates automatically", 174, owner.AutomaticUpdateChecksEnabled);
            automaticUpdates.CheckedChanged += delegate { owner.SetAutomaticUpdateChecks(automaticUpdates.Checked); };
            Controls.Add(automaticUpdates);

            Label actions = SectionLabel("QUICK ACTIONS", 224);
            Controls.Add(actions);

            LiteButton mini = MakeActionButton("Open mini player", 24, 254, 128);
            mini.Click += delegate { owner.ShowMiniPlayer(); };
            Controls.Add(mini);

            LiteButton sleep = MakeActionButton("Sleep now", 160, 254, 105);
            sleep.Click += delegate { owner.SleepNow(); };
            Controls.Add(sleep);

            LiteButton update = MakeActionButton("Check updates", 273, 254, 118);
            update.Click += delegate { owner.CheckForUpdatesInteractive(); };
            Controls.Add(update);

            Label about = SectionLabel("ABOUT", 318);
            Controls.Add(about);

            Label aboutText = new Label();
            aboutText.Text = "YT Music Lite 4.1.0\r\nC# WinForms + Microsoft WebView2\r\nLightweight shell around YouTube Music";
            aboutText.ForeColor = Color.FromArgb(185, 185, 185);
            aboutText.Location = new Point(24, 348);
            aboutText.Size = new Size(330, 58);
            Controls.Add(aboutText);

            LiteButton done = MakeActionButton("Done", 369, 365, 76);
            done.BackColor = Color.FromArgb(255, 46, 70);
            done.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 70, 90);
            done.Click += delegate { Close(); };
            Controls.Add(done);
        }

        private Label SectionLabel(string text, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(24, y);
            label.AutoSize = true;
            label.ForeColor = Color.FromArgb(125, 125, 125);
            label.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            return label;
        }

        private CheckBox MakeCheckBox(string text, int y, bool value)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Checked = value;
            box.Location = new Point(24, y);
            box.Size = new Size(380, 24);
            box.ForeColor = Color.White;
            box.BackColor = BackColor;
            box.FlatStyle = FlatStyle.Flat;
            return box;
        }

        private LiteButton MakeActionButton(string text, int x, int y, int width)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 36);
            return button;
        }
    }
}
