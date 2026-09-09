using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal sealed class ValueSlider : Control
    {
        private double value;
        private bool dragging;
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

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; SetFromX(e.X); Capture = true; base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (dragging) { dragging = false; SetFromX(e.X); Capture = false; Commit(); } base.OnMouseUp(e); }
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
}
