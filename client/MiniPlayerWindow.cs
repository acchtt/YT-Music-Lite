using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed class MiniPlayerWindow : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, int parameter, int value);
        private const int NonClientLeftDown = 0xA1;
        private const int Caption = 0x2;

        private readonly PlaybackEngine playback;
        private readonly Func<Task> previous;
        private readonly Func<Task> next;
        private readonly Action expand;
        private readonly ArtworkControl artwork;
        private readonly Label title;
        private readonly Label artist;
        private readonly Label elapsed;
        private readonly Label duration;
        private readonly IconButton playPause;
        private readonly IconButton pin;
        private readonly ValueSlider progress;
        private bool applying;

        public MiniPlayerWindow(PlaybackEngine engine, Func<Task> previousAction, Func<Task> nextAction, Action expandAction)
        {
            playback = engine;
            previous = previousAction;
            next = nextAction;
            expand = expandAction;
            Text = "YT Music Lite mini player";
            Size = new Size(472, 178);
            MinimumSize = Size;
            MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Sidebar;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterParent;
            TopMost = true;
            ShowInTaskbar = true;
            Padding = new Padding(1);
            AccessibleName = "YT Music Lite mini player";

            Panel border = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border, Padding = new Padding(1) };
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(12) };
            border.Controls.Add(body);
            Controls.Add(border);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Theme.Sidebar };
            Label drag = new Label { Text = "YT MUSIC LITE", Dock = DockStyle.Fill, ForeColor = Theme.Faint, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0) };
            drag.MouseDown += DragWindow;
            header.Controls.Add(drag);
            IconButton close = HeaderButton(AppIcon.Close, "Close mini player"); close.Dock = DockStyle.Right; close.Click += delegate { Close(); };
            IconButton expandButton = HeaderButton(AppIcon.Mini, "Return to main window"); expandButton.Dock = DockStyle.Right; expandButton.Click += delegate { expand(); Close(); };
            pin = HeaderButton(AppIcon.Check, "Always on top"); pin.Dock = DockStyle.Right; pin.Checked = true; pin.Click += delegate { TopMost = !TopMost; pin.Checked = TopMost; pin.Invalidate(); };
            header.Controls.Add(close); header.Controls.Add(expandButton); header.Controls.Add(pin);
            header.BringToFront();
            body.Controls.Add(header);

            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Sidebar, Padding = new Padding(0, 34, 0, 0) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.Controls.Add(content);
            header.BringToFront();
            artwork = new ArtworkControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0), Radius = 10, KeyText = "♪" };
            content.Controls.Add(artwork, 0, 0);

            TableLayoutPanel details = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = Theme.Sidebar };
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            title = new Label { Text = "Nothing playing", Dock = DockStyle.Fill, ForeColor = Theme.Text, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
            artist = new Label { Text = "Choose a song", Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
            details.Controls.Add(title, 0, 0); details.Controls.Add(artist, 0, 1);

            TableLayoutPanel timeline = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Theme.Sidebar };
            timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 37)); timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); timeline.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            elapsed = Time("0:00", ContentAlignment.MiddleRight); duration = Time("0:00", ContentAlignment.MiddleLeft);
            progress = new ValueSlider { Dock = DockStyle.Fill, Maximum = 1, Margin = new Padding(4, 2, 4, 2), AccessibleName = "Song position" };
            progress.ValueCommitted += async delegate { if (!applying) await playback.SeekAsync(progress.Value); };
            timeline.Controls.Add(elapsed, 0, 0); timeline.Controls.Add(progress, 1, 0); timeline.Controls.Add(duration, 2, 0);
            details.Controls.Add(timeline, 0, 2);

            FlowLayoutPanel controls = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Sidebar, Padding = new Padding(80, 0, 0, 0) };
            IconButton previousButton = ControlButton(AppIcon.Previous, "Previous song"); previousButton.Click += async delegate { await previous(); };
            playPause = ControlButton(AppIcon.Play, "Play or pause"); playPause.Accent = true; playPause.Click += async delegate { await playback.TogglePauseAsync(); };
            IconButton nextButton = ControlButton(AppIcon.Next, "Next song"); nextButton.Click += async delegate { await next(); };
            controls.Controls.Add(previousButton); controls.Controls.Add(playPause); controls.Controls.Add(nextButton);
            details.Controls.Add(controls, 0, 3);
            content.Controls.Add(details, 1, 0);
        }

        public void ApplySnapshot(PlaybackSnapshot snapshot)
        {
            if (snapshot == null) return;
            applying = true;
            if (snapshot.Track != null)
            {
                title.Text = snapshot.Track.Title ?? "Untitled";
                artist.Text = string.IsNullOrWhiteSpace(snapshot.Track.Artist) ? "Unknown artist" : snapshot.Track.Artist;
                artwork.KeyText = snapshot.Track.Title;
                artwork.ArtworkUrl = snapshot.Track.ThumbnailUrl;
            }
            playPause.Icon = snapshot.State == PlaybackState.Playing ? AppIcon.Pause : AppIcon.Play;
            playPause.Invalidate();
            double total = snapshot.DurationSeconds > 0 ? snapshot.DurationSeconds : (snapshot.Track == null ? 0 : snapshot.Track.DurationSeconds);
            progress.Maximum = Math.Max(1, total);
            progress.Value = Math.Min(progress.Maximum, snapshot.PositionSeconds);
            elapsed.Text = FormatTime(snapshot.PositionSeconds);
            duration.Text = FormatTime(total);
            applying = false;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            using (System.Drawing.Drawing2D.GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, Width, Height), 14)) Region = new Region(path);
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, NonClientLeftDown, Caption, 0);
        }

        private static IconButton HeaderButton(AppIcon icon, string name)
        {
            return new IconButton { Icon = icon, AccessibleName = name, Width = 30, Height = 28, Margin = Padding.Empty };
        }

        private static IconButton ControlButton(AppIcon icon, string name)
        {
            return new IconButton { Icon = icon, AccessibleName = name, Width = 38, Height = 38, Margin = new Padding(4, 0, 4, 0) };
        }

        private static Label Time(string text, ContentAlignment align)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = Theme.Faint, Font = new Font("Segoe UI", 7.5f), TextAlign = align };
        }

        private static string FormatTime(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds)) return "0:00";
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return string.Format("{0}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }
    }
}
