using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private readonly LiteButton playPause;
        private readonly SeekBar progress;
        private readonly SeekBar volume;
        private readonly ToolTip tips;
        private string artworkUrl = "";
        private bool updating;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        public MiniPlayerForm(MainForm mainForm)
        {
            owner = mainForm;
            Text = "YT Music Lite Mini Player";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(430, 132);
            MinimumSize = Size;
            MaximumSize = Size;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Opacity = 0.99;
            tips = new ToolTip();

            artwork = new PictureBox();
            artwork.Location = new Point(12, 12);
            artwork.Size = new Size(108, 108);
            artwork.SizeMode = PictureBoxSizeMode.Zoom;
            artwork.BackColor = Color.FromArgb(31, 31, 31);
            Controls.Add(artwork);

            title = new Label();
            title.Location = new Point(136, 15);
            title.Size = new Size(240, 23);
            title.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            title.AutoEllipsis = true;
            title.Text = "Nothing playing";
            Controls.Add(title);

            artist = new Label();
            artist.Location = new Point(136, 39);
            artist.Size = new Size(240, 19);
            artist.ForeColor = Color.FromArgb(158, 158, 158);
            artist.AutoEllipsis = true;
            artist.Text = "YouTube Music";
            Controls.Add(artist);

            LiteButton previous = MakeButton("⏮", 136, 67, 38, 34, "Previous");
            playPause = MakeButton("▶", 181, 63, 42, 42, "Play / Pause");
            playPause.BackColor = Color.White;
            playPause.ForeColor = Color.Black;
            playPause.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 230, 230);
            LiteButton next = MakeButton("⏭", 230, 67, 38, 34, "Next");
            LiteButton showMain = MakeButton("▣", 276, 67, 38, 34, "Show main window");
            LiteButton close = MakeButton("×", 391, 7, 30, 28, "Hide mini player");

            previous.Click += delegate { owner.PreviousTrack(); };
            playPause.Click += delegate { owner.TogglePlayback(); };
            next.Click += delegate { owner.NextTrack(); };
            showMain.Click += delegate { owner.RestoreMainWindow(); };
            close.Click += delegate { Hide(); };

            progress = new SeekBar();
            progress.Location = new Point(136, 106);
            progress.Size = new Size(177, 18);
            progress.SeekRequested += delegate
            {
                if (!updating) owner.SeekToRatio(progress.Ratio);
            };
            Controls.Add(progress);

            Label volumeIcon = new Label();
            volumeIcon.Text = "🔊";
            volumeIcon.TextAlign = ContentAlignment.MiddleCenter;
            volumeIcon.ForeColor = Color.FromArgb(170, 170, 170);
            volumeIcon.Location = new Point(320, 103);
            volumeIcon.Size = new Size(26, 22);
            Controls.Add(volumeIcon);

            volume = new SeekBar();
            volume.Location = new Point(346, 106);
            volume.Size = new Size(72, 18);
            volume.Ratio = 1;
            volume.SeekRequested += delegate
            {
                if (!updating) owner.SetVolume(volume.Ratio);
            };
            Controls.Add(volume);

            MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            artist.MouseDown += DragWindow;
            artwork.MouseDown += DragWindow;

            SizeChanged += delegate { ApplyRoundedRegion(); };
            Shown += delegate { ApplyRoundedRegion(); };
            Paint += DrawBorder;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!owner.IsExiting)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        private LiteButton MakeButton(string text, int x, int y, int width, int height, string tip)
        {
            LiteButton button = new LiteButton();
            button.Text = text;
            button.Font = new Font("Segoe UI Symbol", 11f, FontStyle.Regular);
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            Controls.Add(button);
            tips.SetToolTip(button, tip);
            return button;
        }

        private void ApplyRoundedRegion()
        {
            IntPtr region = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 18, 18);
            try
            {
                Region = Region.FromHrgn(region);
            }
            finally
            {
                DeleteObject(region);
            }
        }

        private void DrawBorder(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen border = new Pen(Color.FromArgb(55, 55, 55), 1f))
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 14))
            {
                e.Graphics.DrawPath(border, path);
            }
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
            if (!Visible) Show();
            BringToFront();
            Activate();
        }

        public void UpdatePlayer(PlayerState state)
        {
            if (state == null) return;
            updating = true;
            title.Text = string.IsNullOrWhiteSpace(state.Title) ? "Nothing playing" : state.Title;
            artist.Text = string.IsNullOrWhiteSpace(state.Artist) ? "YouTube Music" : state.Artist;
            playPause.Text = state.Paused ? "▶" : "❚❚";
            progress.Ratio = state.Duration > 0 ? state.CurrentTime / state.Duration : 0;
            progress.Interactive = state.Duration > 0;
            volume.Ratio = state.Volume;
            updating = false;

            if (!string.IsNullOrWhiteSpace(state.ArtworkUrl) && !string.Equals(artworkUrl, state.ArtworkUrl, StringComparison.Ordinal))
            {
                artworkUrl = state.ArtworkUrl;
                try { artwork.LoadAsync(artworkUrl); }
                catch { }
            }
        }
    }
}
