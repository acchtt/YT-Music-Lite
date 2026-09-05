using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class MiniPlayerForm : Form
    {
        private readonly MainForm owner;
        private readonly PictureBox artwork;
        private readonly Label title;
        private readonly Label artist;
        private readonly Button previous;
        private readonly Button playPause;
        private readonly Button next;
        private readonly Button showMain;
        private readonly Button close;
        private readonly TrackBar volume;
        private string artworkUrl = "";
        private bool updatingVolume;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public MiniPlayerForm(MainForm mainForm)
        {
            owner = mainForm;
            Text = "YT Music Lite Mini Player";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(470, 142);
            MinimumSize = Size;
            MaximumSize = Size;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.White;
            Opacity = 0.98;

            artwork = new PictureBox();
            artwork.Location = new Point(10, 10);
            artwork.Size = new Size(122, 122);
            artwork.SizeMode = PictureBoxSizeMode.Zoom;
            artwork.BackColor = Color.FromArgb(35, 35, 35);
            Controls.Add(artwork);

            title = new Label();
            title.Location = new Point(145, 14);
            title.Size = new Size(267, 23);
            title.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            title.AutoEllipsis = true;
            title.Text = "Nothing playing";
            Controls.Add(title);

            artist = new Label();
            artist.Location = new Point(145, 39);
            artist.Size = new Size(267, 20);
            artist.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            artist.ForeColor = Color.Silver;
            artist.AutoEllipsis = true;
            artist.Text = "YouTube Music";
            Controls.Add(artist);

            previous = MakeButton("⏮", 145, 72, 48, 34);
            playPause = MakeButton("▶", 199, 72, 48, 34);
            next = MakeButton("⏭", 253, 72, 48, 34);
            showMain = MakeButton("▣", 307, 72, 48, 34);
            close = MakeButton("×", 425, 7, 34, 27);

            previous.Click += delegate { owner.PreviousTrack(); };
            playPause.Click += delegate { owner.TogglePlayback(); };
            next.Click += delegate { owner.NextTrack(); };
            showMain.Click += delegate { owner.RestoreMainWindow(); };
            close.Click += delegate { Hide(); };

            volume = new TrackBar();
            volume.Location = new Point(145, 111);
            volume.Size = new Size(210, 28);
            volume.Minimum = 0;
            volume.Maximum = 100;
            volume.TickStyle = TickStyle.None;
            volume.Value = 100;
            volume.BackColor = BackColor;
            volume.Scroll += delegate
            {
                if (!updatingVolume)
                {
                    owner.SetVolume(volume.Value / 100.0);
                }
            };
            Controls.Add(volume);

            Label volumeLabel = new Label();
            volumeLabel.Text = "VOL";
            volumeLabel.Location = new Point(360, 115);
            volumeLabel.Size = new Size(38, 18);
            volumeLabel.ForeColor = Color.Gray;
            Controls.Add(volumeLabel);

            MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            artist.MouseDown += DragWindow;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!owner.IsExiting)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        private Button MakeButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 55);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 55);
            button.BackColor = Color.FromArgb(32, 32, 32);
            button.ForeColor = Color.White;
            button.TabStop = false;
            Controls.Add(button);
            return button;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
            }
        }

        public void ShowNearTaskbar()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Right - Width - 18, work.Bottom - Height - 18);
            Show();
            BringToFront();
        }

        public void UpdatePlayer(PlayerState state)
        {
            if (state == null) return;

            title.Text = string.IsNullOrWhiteSpace(state.Title) ? "Nothing playing" : state.Title;
            artist.Text = string.IsNullOrWhiteSpace(state.Artist) ? "YouTube Music" : state.Artist;
            playPause.Text = state.Paused ? "▶" : "❚❚";

            int value = (int)Math.Round(state.Volume * 100.0);
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            updatingVolume = true;
            volume.Value = value;
            updatingVolume = false;

            if (!string.IsNullOrWhiteSpace(state.ArtworkUrl) && !string.Equals(artworkUrl, state.ArtworkUrl, StringComparison.Ordinal))
            {
                artworkUrl = state.ArtworkUrl;
                try
                {
                    artwork.LoadAsync(artworkUrl);
                }
                catch
                {
                }
            }
        }
    }
}
