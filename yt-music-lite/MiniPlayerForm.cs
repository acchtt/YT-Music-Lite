using System;
using System.Drawing;
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
        private readonly CheckBox pin;
        private readonly ToolTip tips = new ToolTip();
        private string artworkUrl = "";
        private bool positioning;

        public MiniPlayerForm(MainForm mainForm)
        {
            owner = mainForm;
            AutoScaleDimensions = new SizeF(6f, 13f);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "YT Music Lite Mini Player";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(460, 185);
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = owner.MiniAlwaysOnTop;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            artwork = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(31, 31, 31), Margin = new Padding(0, 0, 12, 12) };
            layout.Controls.Add(artwork, 0, 0);
            layout.SetRowSpan(artwork, 3);
            title = new Label { Text = "Nothing playing", Dock = DockStyle.Fill, AutoEllipsis = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
            artist = new Label { Text = "Choose a song in YouTube Music", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.FromArgb(175, 175, 175) };
            layout.Controls.Add(title, 1, 0);
            layout.Controls.Add(artist, 1, 1);
            FlowLayoutPanel transport = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            layout.Controls.Add(transport, 1, 2);
            MakeButton(transport, "⏮", "Previous track", delegate { owner.PreviousTrack(); });
            playPause = MakeButton(transport, "▶", "Play", delegate { owner.TogglePlayback(); });
            playPause.BackColor = Color.White;
            playPause.ForeColor = Color.Black;
            MakeButton(transport, "⏭", "Next track", delegate { owner.NextTrack(); });
            MakeButton(transport, "▣", "Show main window", delegate { owner.RestoreMainWindow(); });
            pin = new CheckBox { Text = "On top", Checked = TopMost, AutoSize = true, Margin = new Padding(8, 12, 0, 0), AccessibleName = "Always on top" };
            pin.CheckedChanged += delegate { owner.SetMiniAlwaysOnTop(pin.Checked); };
            tips.SetToolTip(pin, "Keep the mini player above other windows");
            transport.Controls.Add(pin);

            TableLayoutPanel timeline = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
            timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            timeline.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            timeline.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            timeline.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(timeline, 0, 3);
            layout.SetColumnSpan(timeline, 2);
            progress = new SeekBar { Dock = DockStyle.Fill, AccessibleName = "Playback position", Interactive = false };
            progress.SeekRequested += delegate { owner.SeekToRatio(progress.Ratio); };
            timeline.Controls.Add(progress, 0, 0);
            Label volumeLabel = new Label { Text = "Volume", AutoSize = true, Anchor = AnchorStyles.None, ForeColor = Color.FromArgb(175, 175, 175) };
            timeline.Controls.Add(volumeLabel, 1, 0);
            volume = new SeekBar { Dock = DockStyle.Fill, AccessibleName = "Volume", Ratio = 1 };
            volume.SeekRequested += delegate { owner.SetVolume(volume.Ratio); };
            timeline.Controls.Add(volume, 2, 0);
            time = new Label { Text = "0:00 / 0:00", AutoSize = true, ForeColor = Color.FromArgb(175, 175, 175) };
            timeline.Controls.Add(time, 0, 1);

            // The native caption is the drag handle; artwork/title retain their click action.
            ResizeEnd += delegate { if (!positioning && Visible) owner.RememberMiniLocation(Location); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!owner.IsExiting) { e.Cancel = true; owner.RememberMiniLocation(Location); Hide(); }
            };
        }

        private LiteButton MakeButton(Control parent, string text, string name, Action click)
        {
            LiteButton button = new LiteButton { Text = text, AccessibleName = name, Size = new Size(38, 38), Margin = new Padding(0, 0, 5, 0) };
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
            pin.Checked = owner.MiniAlwaysOnTop;
            positioning = false;
            BringToFront();
            Activate();
        }

        public void UpdatePlayer(PlayerState state)
        {
            if (state == null) return;
            title.Text = string.IsNullOrWhiteSpace(state.Title) ? "Nothing playing" : state.Title;
            artist.Text = string.IsNullOrWhiteSpace(state.Artist) ? "Choose a song in YouTube Music" : state.Artist;
            playPause.AccessibleName = state.Paused ? "Play" : "Pause";
            playPause.Text = state.Paused ? "▶" : "❚❚";
            progress.Duration = state.Duration;
            progress.Ratio = state.Duration > 0 ? state.CurrentTime / state.Duration : 0;
            progress.Interactive = state.Duration > 0;
            volume.Ratio = state.Volume;
            time.Text = SeekBar.FormatTime(state.CurrentTime) + " / " + SeekBar.FormatTime(state.Duration);
            if (!string.Equals(artworkUrl, state.ArtworkUrl, StringComparison.Ordinal))
            {
                artworkUrl = state.ArtworkUrl ?? "";
                artwork.CancelAsync();
                if (string.IsNullOrWhiteSpace(artworkUrl)) artwork.Image = null;
                else { try { artwork.LoadAsync(artworkUrl); } catch { artwork.Image = null; } }
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }
}
