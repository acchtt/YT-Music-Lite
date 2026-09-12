using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed class PlaylistNavigationControl : Control
    {
        private const int RowHeight = 42;
        private static readonly Font RowFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        private readonly List<Playlist> playlists = new List<Playlist>();
        private int scrollOffset;
        private int hoveredIndex = -1;
        private string selectedPlaylistId;
        private bool draggingScrollbar;
        public event Action<Playlist> PlaylistActivated;
        public int PlaylistCount { get { return playlists.Count; } }
        public string SelectedPlaylistId
        {
            get { return selectedPlaylistId; }
            set { selectedPlaylistId = value; EnsureSelectedVisible(); Invalidate(); }
        }

        public PlaylistNavigationControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            BackColor = Theme.Sidebar;
            TabStop = true;
        }

        public void SetPlaylists(IEnumerable<Playlist> values)
        {
            playlists.Clear();
            if (values != null) playlists.AddRange(values);
            scrollOffset = Math.Min(scrollOffset, MaximumScroll());
            hoveredIndex = -1;
            EnsureSelectedVisible();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.EnableQuality(e.Graphics);
            int contentWidth = Math.Max(0, Width - (MaximumScroll() > 0 ? 12 : 0));
            int first = scrollOffset / RowHeight;
            int y = first * RowHeight - scrollOffset;
            for (int index = first; index < playlists.Count && y < Height; index++, y += RowHeight)
            {
                Playlist playlist = playlists[index];
                bool selected = string.Equals(playlist.Id, selectedPlaylistId, StringComparison.OrdinalIgnoreCase);
                Rectangle row = new Rectangle(4, y + 2, Math.Max(0, contentWidth - 8), RowHeight - 4);
                if (selected || index == hoveredIndex || (Focused && index == SelectedIndex()))
                {
                    using (SolidBrush background = new SolidBrush(selected ? Theme.SurfaceSelected : Theme.SurfaceHover))
                    using (GraphicsPath path = Theme.Rounded(row, 9)) e.Graphics.FillPath(background, path);
                }
                if (selected)
                {
                    using (SolidBrush accent = new SolidBrush(Theme.Accent))
                    using (GraphicsPath marker = Theme.Rounded(new Rectangle(4, y + 10, 3, 22), 2)) e.Graphics.FillPath(accent, marker);
                }
                IconPainter.Draw(e.Graphics, AppIcon.Playlist, new Rectangle(16, y + 11, 20, 20), selected ? Theme.Text : Theme.Muted, 1.8f);
                TextRenderer.DrawText(e.Graphics, playlist.Name ?? "Untitled playlist", RowFont, new Rectangle(47, y, Math.Max(0, contentWidth - 55), RowHeight), selected ? Theme.Text : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            DrawScrollbar(e.Graphics);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (draggingScrollbar)
            {
                ScrollFromPointer(e.Y);
                return;
            }
            int next = e.X >= Width - 12 ? -1 : IndexAt(e.Y);
            if (next != hoveredIndex) { hoveredIndex = next; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoveredIndex = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { SetScroll(scrollOffset - Math.Sign(e.Delta) * RowHeight * 3); base.OnMouseWheel(e); }
        protected override void OnResize(EventArgs e) { SetScroll(scrollOffset); base.OnResize(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                if (MaximumScroll() > 0 && e.X >= Width - 12)
                {
                    draggingScrollbar = true;
                    Capture = true;
                    ScrollFromPointer(e.Y);
                }
                else Activate(IndexAt(e.Y));
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (draggingScrollbar) { draggingScrollbar = false; Capture = false; }
            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) draggingScrollbar = false; base.OnMouseCaptureChanged(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int selected = SelectedIndex();
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                int next = selected < 0 ? 0 : Math.Max(0, Math.Min(playlists.Count - 1, selected + (e.KeyCode == Keys.Up ? -1 : 1)));
                if (playlists.Count > 0) SelectedPlaylistId = playlists[next].Id;
                e.Handled = true;
            }
            else if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && selected >= 0) { Activate(selected); e.Handled = true; }
            base.OnKeyDown(e);
        }

        private void Activate(int index)
        {
            if (index < 0 || index >= playlists.Count) return;
            SelectedPlaylistId = playlists[index].Id;
            Action<Playlist> handler = PlaylistActivated;
            if (handler != null) handler(playlists[index]);
        }

        private int IndexAt(int y)
        {
            int index = (y + scrollOffset) / RowHeight;
            return y < 0 || index < 0 || index >= playlists.Count ? -1 : index;
        }

        private int SelectedIndex()
        {
            for (int index = 0; index < playlists.Count; index++) if (string.Equals(playlists[index].Id, selectedPlaylistId, StringComparison.OrdinalIgnoreCase)) return index;
            return -1;
        }

        private void EnsureSelectedVisible()
        {
            int index = SelectedIndex();
            if (index < 0 || Height <= 0) return;
            int top = index * RowHeight;
            if (top < scrollOffset) SetScroll(top);
            else if (top + RowHeight > scrollOffset + Height) SetScroll(top + RowHeight - Height);
        }

        private int MaximumScroll() { return Math.Max(0, playlists.Count * RowHeight - Math.Max(0, Height)); }
        private void SetScroll(int value) { int next = Math.Max(0, Math.Min(MaximumScroll(), value)); if (next != scrollOffset) { scrollOffset = next; Invalidate(); } }

        private void ScrollFromPointer(int y)
        {
            int maximum = MaximumScroll();
            if (maximum <= 0) return;
            int thumbHeight = Math.Max(30, Height * Height / Math.Max(Height, playlists.Count * RowHeight));
            int travel = Math.Max(1, Height - 8 - thumbHeight);
            double ratio = Math.Max(0, Math.Min(1, (y - 4 - thumbHeight / 2d) / travel));
            SetScroll((int)Math.Round(maximum * ratio));
        }

        private void DrawScrollbar(Graphics graphics)
        {
            int maximum = MaximumScroll();
            if (maximum <= 0 || Height < 20) return;
            int thumbHeight = Math.Max(30, Height * Height / Math.Max(Height, playlists.Count * RowHeight));
            int travel = Math.Max(1, Height - 8 - thumbHeight);
            int top = 4 + (int)Math.Round((double)scrollOffset / maximum * travel);
            using (SolidBrush track = new SolidBrush(Theme.Surface)) graphics.FillRectangle(track, Width - 7, 4, 3, Height - 8);
            using (SolidBrush thumb = new SolidBrush(draggingScrollbar ? Theme.Accent : Theme.Faint))
            using (GraphicsPath path = Theme.Rounded(new Rectangle(Width - 8, top, 5, thumbHeight), 3)) graphics.FillPath(thumb, path);
        }
    }

    internal sealed class ValueSlider : Control
    {
        private double value;
        private bool dragging;
        public bool IsDragging { get { return dragging; } }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Value
        {
            get { return value; }
            set { this.value = Math.Max(Minimum, Math.Min(Maximum, value)); Invalidate(); }
        }
        public event EventHandler ValueCommitted;

        public ValueSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Height = 26;
            Minimum = 0;
            Maximum = 100;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); dragging = true; Capture = true; SetFromX(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (dragging) { dragging = false; SetFromX(e.X); Capture = false; Commit(); } base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { if (dragging && !Capture) { dragging = false; Commit(); } base.OnMouseCaptureChanged(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            double step = e.Control ? (Maximum - Minimum) / 100d : (Maximum - Minimum) / 20d;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Value -= step; Commit(); e.Handled = true; }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Value += step; Commit(); e.Handled = true; }
            else if (e.KeyCode == Keys.Home) { Value = Minimum; Commit(); e.Handled = true; }
            else if (e.KeyCode == Keys.End) { Value = Maximum; Commit(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            int left = 7;
            int right = Width - 7;
            int y = Height / 2;
            float ratio = Maximum <= Minimum ? 0 : (float)((Value - Minimum) / (Maximum - Minimum));
            int fill = left + (int)((right - left) * ratio);
            using (Pen rail = new Pen(Theme.Border, 4)) { rail.StartCap = LineCap.Round; rail.EndCap = LineCap.Round; e.Graphics.DrawLine(rail, left, y, right, y); }
            using (Pen accent = new Pen(Theme.Accent, 4)) { accent.StartCap = LineCap.Round; accent.EndCap = LineCap.Round; e.Graphics.DrawLine(accent, left, y, fill, y); }
            using (SolidBrush knob = new SolidBrush(Focused || dragging ? Color.White : Theme.Text)) e.Graphics.FillEllipse(knob, fill - 5, y - 5, 10, 10);
        }

        private void SetFromX(int x)
        {
            double ratio = Math.Max(0, Math.Min(1, (x - 7d) / Math.Max(1, Width - 14d)));
            Value = Minimum + ratio * (Maximum - Minimum);
        }

        private void Commit()
        {
            EventHandler handler = ValueCommitted;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    internal sealed class BrandControl : Control
    {
        public BrandControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 72;
            Dock = DockStyle.Top;
            AccessibleName = "YT Music Lite";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            Branding.DrawMark(e.Graphics, new RectangleF(18, 17, 38, 38));
            using (Font title = new Font("Segoe UI", 11, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, "YT MUSIC", title, new Rectangle(68, 13, Width - 76, 28), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Font lite = new Font("Segoe UI", 8, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, "LITE", lite, new Rectangle(69, 39, Width - 76, 18), Theme.Accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class HomeTile : Control
    {
        private bool hovered;
        public Track Track { get; set; }

        public HomeTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Width = 210;
            Height = 76;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            using (SolidBrush background = new SolidBrush(hovered || Focused ? Theme.SurfaceSelected : Theme.Surface)) using (GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12)) e.Graphics.FillPath(background, path);
            Track track = Track;
            if (track == null) return;
            Rectangle art = new Rectangle(8, 8, 60, 60);
            Image image = ArtworkCache.Get(track.ThumbnailUrl, delegate { try { if (IsHandleCreated) BeginInvoke((Action)Invalidate); } catch { } });
            using (GraphicsPath clip = Theme.Rounded(art, 8)) { e.Graphics.SetClip(clip); if (image != null) e.Graphics.DrawImage(image, art); else ArtworkControl.DrawPlaceholder(e.Graphics, art, track.Title); e.Graphics.ResetClip(); }
            using (Font title = new Font("Segoe UI", 9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, track.Title ?? "Untitled", title, new Rectangle(80, 16, Width - 92, 24), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Font artist = new Font("Segoe UI", 8.3f)) TextRenderer.DrawText(e.Graphics, track.Artist ?? "Unknown artist", artist, new Rectangle(80, 39, Width - 92, 20), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class PlaylistCard : Control
    {
        private bool hovered;
        public Playlist Playlist { get; set; }

        public PlaylistCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Width = 270;
            Height = 96;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            using (SolidBrush background = new SolidBrush(hovered || Focused ? Theme.SurfaceSelected : Theme.Surface))
            using (GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 13)) e.Graphics.FillPath(background, path);
            Playlist playlist = Playlist;
            if (playlist == null) return;
            Rectangle art = new Rectangle(10, 10, 76, 76);
            Track cover = playlist.Tracks == null || playlist.Tracks.Count == 0 ? null : playlist.Tracks[0];
            string artwork = cover == null ? playlist.ThumbnailUrl : cover.ThumbnailUrl;
            Image image = ArtworkCache.Get(artwork, delegate { try { if (IsHandleCreated) BeginInvoke((Action)Invalidate); } catch { } });
            using (GraphicsPath clip = Theme.Rounded(art, 8))
            {
                e.Graphics.SetClip(clip);
                if (image != null) e.Graphics.DrawImage(image, art); else ArtworkControl.DrawPlaceholder(e.Graphics, art, playlist.Name);
                e.Graphics.ResetClip();
            }
            using (Font title = new Font("Segoe UI", 10, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, playlist.Name ?? "Untitled playlist", title, new Rectangle(101, 17, Width - 115, 25), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            int total = playlist.TracksLoaded ? (playlist.Tracks == null ? 0 : playlist.Tracks.Count) : playlist.TrackCount;
            string count = total > 0 ? total + (total == 1 ? " song" : " songs") : (playlist.TracksLoaded ? "0 songs" : "Open to load songs");
            using (Font body = new Font("Segoe UI", 8.7f)) TextRenderer.DrawText(e.Graphics, count, body, new Rectangle(101, 43, Width - 115, 20), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (playlist.IsRemote) using (Font source = new Font("Segoe UI", 7.8f, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, "YOUTUBE", source, new Rectangle(101, 65, Width - 115, 17), Theme.Accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
