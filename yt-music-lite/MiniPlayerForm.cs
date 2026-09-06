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
        private readonly Label time;
        private readonly LiteButton pin;
        private readonly Label playbackLabel;
        private readonly Label remaining;
        private readonly LiteButton mute;
        private double lastVolume = 1;
        private double audibleVolume = 1;
        private readonly ToolTip tips = new ToolTip();
        private string artworkUrl = "";
        private bool positioning;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);

        public MiniPlayerForm(MainForm mainForm)
        {
            owner = mainForm;
            AutoScaleDimensions = new SizeF(7f, 15f);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "YT Music Lite Mini Player";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(440, 272);
            TopMost = owner.MiniAlwaysOnTop;
            ShowInTaskbar = false;
            BackColor = BrandArt.Surface;
            ForeColor = Color.FromArgb(245, 246, 251);
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; } };

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 18, 14), ColumnCount = 1, RowCount = 5, BackColor = BackColor };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            BrandLogoControl logo = new BrandLogoControl { MarkOnly = true, Size = new Size(22, 22), Anchor = AnchorStyles.Left, Margin = Padding.Empty };
            header.Controls.Add(logo, 0, 0);
            playbackLabel = new Label { Text = "READY TO PLAY", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = BrandArt.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(5, 0, 0, 0) };
            header.Controls.Add(playbackLabel, 1, 0);
            FlowLayoutPanel utilities = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            pin = MakeButton(utilities, "Pin", "Always on top", delegate { owner.SetMiniAlwaysOnTop(!owner.MiniAlwaysOnTop); UpdatePin(); });
            pin.DrawnIcon = IconKind.Pin;
            pin.Size = new Size(28, 28);
            LiteButton expand = MakeButton(utilities, "▣", "Show main window", delegate { owner.RestoreMainWindow(); });
            expand.Size = new Size(28, 28);
            LiteButton close = MakeButton(utilities, "×", "Hide mini player", delegate { owner.RememberMiniLocation(Location); Hide(); });
            close.Size = new Size(28, 28);
            header.Controls.Add(utilities, 2, 0);
            layout.Controls.Add(header, 0, 0);
            header.MouseDown += DragWindow;
            playbackLabel.MouseDown += DragWindow;
            logo.MouseDown += DragWindow;
            tips.SetToolTip(playbackLabel, "Drag to move the mini player");

            TableLayoutPanel track = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 4, 0, 0) };
            track.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            track.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            track.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            track.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            artwork = new RoundedArtwork { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(33, 35, 44), Margin = new Padding(0, 0, 14, 0) };
            track.Controls.Add(artwork, 0, 0);
            track.SetRowSpan(artwork, 2);
            title = new Label { Text = "Your next favorite", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Margin = new Padding(0, 2, 0, 0) };
            artist = new Label { Text = "Choose a song to get started", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = BrandArt.Muted, Font = new Font("Segoe UI", 9.5f), Margin = Padding.Empty };
            track.Controls.Add(title, 1, 0);
            track.Controls.Add(artist, 1, 1);
            layout.Controls.Add(track, 0, 1);

            progress = new SeekBar { Dock = DockStyle.Fill, AccessibleName = "Playback position", Interactive = false, Margin = new Padding(0, 6, 0, 0) };
            progress.SeekRequested += delegate { owner.SeekToRatio(progress.Ratio); };
            layout.Controls.Add(progress, 0, 2);
            TableLayoutPanel times = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            time = new Label { Text = "0:00", Dock = DockStyle.Fill, ForeColor = BrandArt.Muted, Font = new Font("Segoe UI", 8f), Margin = Padding.Empty };
            remaining = new Label { Text = "0:00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight, ForeColor = BrandArt.Muted, Font = time.Font, Margin = Padding.Empty };
            times.Controls.Add(time, 0, 0);
            times.Controls.Add(remaining, 1, 0);
            layout.Controls.Add(times, 0, 3);

            TableLayoutPanel footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            FlowLayoutPanel transport = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
            LiteButton previous = MakeButton(transport, "⏮", "Previous track", delegate { owner.PreviousTrack(); });
            previous.Margin = new Padding(0, 9, 8, 0);
            playPause = MakeButton(transport, "▶", "Play", delegate { owner.TogglePlayback(); });
            playPause.Size = new Size(50, 50);
            playPause.IconStyle = IconButtonStyle.Accent;
            playPause.Margin = new Padding(0, 0, 8, 0);
            LiteButton next = MakeButton(transport, "⏭", "Next track", delegate { owner.NextTrack(); });
            next.Margin = new Padding(0, 9, 0, 0);
            footer.Controls.Add(transport, 0, 0);
            FlowLayoutPanel sound = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            mute = MakeButton(sound, "Mute", "Mute", delegate { owner.SetVolume(lastVolume > 0 ? 0 : audibleVolume); });
            mute.DrawnIcon = IconKind.Volume;
            volume = new SeekBar { Size = new Size(88, 24), AccessibleName = "Volume", Ratio = 1, Margin = new Padding(2, 4, 0, 0) };
            volume.SeekRequested += delegate { owner.SetVolume(volume.Ratio); };
            sound.Controls.Add(volume);
            footer.Controls.Add(sound, 2, 0);
            layout.Controls.Add(footer, 0, 4);

            UpdatePin();
            SizeChanged += delegate { RoundWindow(); };
            Shown += delegate { RoundWindow(); };
            ResizeEnd += delegate { if (!positioning && Visible) owner.RememberMiniLocation(Location); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!owner.IsExiting) { e.Cancel = true; owner.RememberMiniLocation(Location); Hide(); }
            };
        }

        private void UpdatePin()
        {
            pin.Selected = owner.MiniAlwaysOnTop;
            pin.AccessibleName = owner.MiniAlwaysOnTop ? "Always on top: on" : "Always on top: off";
            tips.SetToolTip(pin, pin.AccessibleName);
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
            owner.RememberMiniLocation(Location);
        }

        private void RoundWindow()
        {
            if (Width < 1 || Height < 1) return;
            Region old = Region;
            using (GraphicsPath path = BrandArt.Rounded(new RectangleF(0, 0, Width, Height), 16)) Region = new Region(path);
            if (old != null) old.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen border = new Pen(Color.FromArgb(57, 60, 73)))
            using (GraphicsPath path = BrandArt.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 16)) e.Graphics.DrawPath(border, path);
        }

        private LiteButton MakeButton(Control parent, string text, string name, Action click)
        {
            LiteButton button = new LiteButton { Text = text, AccessibleName = name, Size = new Size(32, 32), Margin = new Padding(0, 0, 5, 0) };
            button.Click += delegate { click(); };
            tips.SetToolTip(button, name);
            parent.Controls.Add(button);
            return button;
        }

        public static Point ClampToWorkingArea(Point location, Size size, Rectangle work)
        {
            return new Point(Math.Max(work.Left, Math.Min(location.X, work.Right - size.Width)), Math.Max(work.Top, Math.Min(location.Y, work.Bottom - size.Height)));
        }

        public void ShowNearTaskbar()
        {
            positioning = true;
            // Show first so initial font/DPI scaling has determined the final bounds.
            if (!Visible) Show();
            Rectangle work = owner.MiniLocation.HasValue ? Screen.FromPoint(owner.MiniLocation.Value).WorkingArea : Screen.FromControl(owner).WorkingArea;
            Point desired = owner.MiniLocation ?? new Point(work.Right - Width - 18, work.Bottom - Height - 18);
            Location = ClampToWorkingArea(desired, Size, work);
            UpdatePin();
            positioning = false;
            BringToFront();
            Activate();
        }

        public void UpdatePlayer(PlayerState state)
        {
            if (state == null) return;
            title.Text = string.IsNullOrWhiteSpace(state.Title) || state.Title == "Nothing playing" ? "Your next favorite" : state.Title;
            artist.Text = state.Duration <= 0 ? "Choose a song to get started" : state.Artist;
            playbackLabel.Text = state.Duration <= 0 ? "READY TO PLAY" : state.Paused ? "PAUSED" : "NOW PLAYING";
            UpdatePin();
            playPause.AccessibleName = state.Paused ? "Play" : "Pause";
            playPause.Text = state.Paused ? "▶" : "❚❚";
            progress.Duration = state.Duration;
            progress.Ratio = state.Duration > 0 ? state.CurrentTime / state.Duration : 0;
            progress.Interactive = state.Duration > 0;
            volume.Ratio = state.Volume;
            time.Text = SeekBar.FormatTime(state.CurrentTime);
            remaining.Text = SeekBar.FormatTime(state.Duration);
            lastVolume = state.Volume;
            if (state.Volume > 0) audibleVolume = state.Volume;
            mute.DrawnIcon = state.Volume > 0 ? IconKind.Volume : IconKind.Muted;
            mute.AccessibleName = state.Volume > 0 ? "Mute" : "Unmute";
            tips.SetToolTip(mute, mute.AccessibleName);
            if (!string.Equals(artworkUrl, state.ArtworkUrl, StringComparison.Ordinal))
            {
                artworkUrl = state.ArtworkUrl ?? "";
                artwork.CancelAsync();
                if (string.IsNullOrWhiteSpace(artworkUrl)) artwork.Image = null;
                else { try { artwork.LoadAsync(artworkUrl); } catch { artwork.Image = null; } }
            }
        }

        private sealed class RoundedArtwork : PictureBox
        {
            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                if (Width <= 0 || Height <= 0) return;
                Region old = Region;
                using (GraphicsPath path = BrandArt.Rounded(new RectangleF(0, 0, Width, Height), 12)) Region = new Region(path);
                if (old != null) old.Dispose();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Image != null) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen ring = new Pen(Color.FromArgb(49, 52, 65)))
                {
                    e.Graphics.DrawEllipse(ring, 8, 8, Width - 16, Height - 16);
                    e.Graphics.DrawEllipse(ring, 18, 18, Width - 36, Height - 36);
                }
                float size = Math.Min(Width, Height) * 0.46f;
                BrandArt.DrawMark(e.Graphics, new RectangleF((Width - size) / 2, (Height - size) / 2, size, size));
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }
}
