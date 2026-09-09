using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed class TrackListControl : Control
    {
        private readonly VScrollBar scroll;
        private List<Track> items = new List<Track>();
        private int selectedIndex = -1;
        private int hoverIndex = -1;
        private const int RowHeight = 64;

        public event EventHandler SelectionChanged;
        public event EventHandler TrackActivated;
        public event EventHandler ContextRequested;

        public TrackListControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            TabStop = true;
            AccessibleName = "Songs";
            scroll = new VScrollBar();
            scroll.Dock = DockStyle.Right;
            scroll.SmallChange = RowHeight;
            scroll.LargeChange = RowHeight * 5;
            scroll.ValueChanged += delegate { Invalidate(); };
            Controls.Add(scroll);
        }

        public IList<Track> Items { get { return items.AsReadOnly(); } }
        public Track SelectedTrack { get { return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null; } }
        public int SelectedIndex { get { return selectedIndex; } }

        public void SetTracks(IEnumerable<Track> tracks)
        {
            items = tracks == null ? new List<Track>() : new List<Track>(tracks);
            selectedIndex = -1;
            hoverIndex = -1;
            UpdateScroll();
            Invalidate();
        }

        public void SelectIndex(int index)
        {
            if (index < 0 || index >= items.Count) index = -1;
            selectedIndex = index;
            EnsureVisible(index);
            OnSelectionChanged();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScroll();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int next = scroll.Value - Math.Sign(e.Delta) * RowHeight * 3;
            scroll.Value = Math.Max(scroll.Minimum, Math.Min(MaximumValue(), next));
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexAt(e.Y);
            if (index != hoverIndex) { hoverIndex = index; Invalidate(); }
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            int index = IndexAt(e.Y);
            if (index >= 0)
            {
                selectedIndex = index;
                OnSelectionChanged();
                if (e.Button == MouseButtons.Right) { EventHandler handler = ContextRequested; if (handler != null) handler(this, EventArgs.Empty); }
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (IndexAt(e.Y) >= 0) { EventHandler handler = TrackActivated; if (handler != null) handler(this, EventArgs.Empty); }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { SelectIndex(Math.Min(items.Count - 1, selectedIndex + 1)); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { SelectIndex(Math.Max(0, selectedIndex - 1)); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter && SelectedTrack != null) { EventHandler handler = TrackActivated; if (handler != null) handler(this, EventArgs.Empty); e.Handled = true; }
            else if ((e.KeyCode == Keys.Apps || (e.Shift && e.KeyCode == Keys.F10)) && SelectedTrack != null) { EventHandler handler = ContextRequested; if (handler != null) handler(this, EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            e.Graphics.Clear(BackColor);
            if (items.Count == 0)
            {
                using (Font title = new Font("Segoe UI", 13, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, "Nothing here yet", title, new Rectangle(24, 38, Width - 48, 32), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                using (Font body = new Font("Segoe UI", 9.5f)) TextRenderer.DrawText(e.Graphics, "Search for music or import audio from this computer.", body, new Rectangle(24, 72, Width - 48, 30), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            int first = scroll.Value / RowHeight;
            int offset = -(scroll.Value % RowHeight);
            int visible = Height / RowHeight + 2;
            for (int i = first; i < Math.Min(items.Count, first + visible); i++) DrawRow(e.Graphics, i, offset + (i - first) * RowHeight);
        }

        private void DrawRow(Graphics graphics, int index, int y)
        {
            Track track = items[index];
            Rectangle row = new Rectangle(4, y + 2, Width - scroll.Width - 12, RowHeight - 4);
            if (index == selectedIndex || index == hoverIndex)
            {
                using (SolidBrush brush = new SolidBrush(index == selectedIndex ? Theme.SurfaceSelected : Theme.SurfaceHover))
                using (System.Drawing.Drawing2D.GraphicsPath path = Theme.Rounded(row, 9)) graphics.FillPath(brush, path);
            }
            Rectangle artwork = new Rectangle(row.X + 42, row.Y + 8, 44, 44);
            Image image = ArtworkCache.Get(track.ThumbnailUrl, delegate { try { if (IsHandleCreated) BeginInvoke((Action)Invalidate); } catch { } });
            using (System.Drawing.Drawing2D.GraphicsPath clip = Theme.Rounded(artwork, 6))
            {
                graphics.SetClip(clip);
                if (image != null) graphics.DrawImage(image, artwork); else ArtworkControl.DrawPlaceholder(graphics, artwork, track.Title);
                graphics.ResetClip();
            }
            using (Font number = new Font("Segoe UI", 9)) TextRenderer.DrawText(graphics, (index + 1).ToString(), number, new Rectangle(row.X + 4, row.Y, 28, row.Height), index == selectedIndex ? Theme.Accent : Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            int textLeft = artwork.Right + 13;
            int durationWidth = 74;
            using (Font title = new Font("Segoe UI", 9.5f, FontStyle.Bold)) TextRenderer.DrawText(graphics, track.Title ?? "Untitled", title, new Rectangle(textLeft, row.Y + 8, row.Right - textLeft - durationWidth, 24), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Font artist = new Font("Segoe UI", 8.8f)) TextRenderer.DrawText(graphics, string.IsNullOrWhiteSpace(track.Artist) ? "Unknown artist" : track.Artist, artist, new Rectangle(textLeft, row.Y + 31, row.Right - textLeft - durationWidth, 20), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Font duration = new Font("Segoe UI", 8.5f)) TextRenderer.DrawText(graphics, FormatTime(track.DurationSeconds), duration, new Rectangle(row.Right - durationWidth, row.Y, durationWidth - 14, row.Height), Theme.Faint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private int IndexAt(int y)
        {
            int index = (y + scroll.Value) / RowHeight;
            return index >= 0 && index < items.Count ? index : -1;
        }

        private void EnsureVisible(int index)
        {
            if (index < 0) return;
            int top = index * RowHeight;
            int bottom = top + RowHeight;
            if (top < scroll.Value) scroll.Value = Math.Max(scroll.Minimum, top);
            else if (bottom > scroll.Value + Height) scroll.Value = Math.Min(MaximumValue(), bottom - Height);
        }

        private void UpdateScroll()
        {
            scroll.Minimum = 0;
            scroll.Maximum = Math.Max(0, items.Count * RowHeight - 1);
            scroll.LargeChange = Math.Max(RowHeight, Height);
            scroll.Visible = items.Count * RowHeight > Height;
            if (scroll.Value > MaximumValue()) scroll.Value = MaximumValue();
        }

        private int MaximumValue()
        {
            return Math.Max(0, scroll.Maximum - scroll.LargeChange + 1);
        }

        private void OnSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static string FormatTime(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds)) return "—";
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1 ? string.Format("{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds) : string.Format("{0}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }
    }
}
