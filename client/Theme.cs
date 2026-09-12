using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal static class Theme
    {
        public static readonly Color Window = Color.FromArgb(10, 11, 14);
        public static readonly Color Sidebar = Color.FromArgb(14, 15, 20);
        public static readonly Color Surface = Color.FromArgb(22, 24, 30);
        public static readonly Color SurfaceHover = Color.FromArgb(31, 34, 42);
        public static readonly Color SurfaceSelected = Color.FromArgb(42, 45, 55);
        public static readonly Color Border = Color.FromArgb(49, 52, 62);
        public static readonly Color Text = Color.FromArgb(245, 246, 249);
        public static readonly Color Muted = Color.FromArgb(157, 161, 174);
        public static readonly Color Faint = Color.FromArgb(100, 104, 117);
        public static readonly Color Accent = Color.FromArgb(255, 73, 101);
        public static readonly Color AccentHover = Color.FromArgb(255, 101, 124);
        public static readonly Color Danger = Color.FromArgb(255, 104, 104);

        public static GraphicsPath Rounded(Rectangle rectangle, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void EnableQuality(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    internal enum AppIcon
    {
        Home,
        Discover,
        Search,
        Library,
        Playlist,
        Queue,
        Settings,
        Play,
        Pause,
        Previous,
        Next,
        Stop,
        Add,
        Heart,
        More,
        Volume,
        VolumeMuted,
        Shuffle,
        Repeat,
        RepeatOne,
        Mini,
        Close,
        Back,
        Forward,
        Download,
        Check
    }

    internal static class IconPainter
    {
        public static void Draw(Graphics graphics, AppIcon icon, Rectangle bounds, Color color, float width)
        {
            Theme.EnableQuality(graphics);
            float scale = Math.Min(bounds.Width, bounds.Height) / 24f;
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(bounds.X + (bounds.Width - 24f * scale) / 2f, bounds.Y + (bounds.Height - 24f * scale) / 2f);
            graphics.ScaleTransform(scale, scale);
            using (Pen pen = new Pen(color, width / Math.Max(scale, 0.01f)))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (icon)
                {
                    case AppIcon.Home:
                        graphics.DrawLines(pen, new PointF[] { new PointF(3, 11), new PointF(12, 3), new PointF(21, 11) });
                        graphics.DrawLines(pen, new PointF[] { new PointF(5, 10), new PointF(5, 21), new PointF(19, 21), new PointF(19, 10) });
                        graphics.DrawLine(pen, 10, 21, 10, 15); graphics.DrawLine(pen, 14, 21, 14, 15); break;
                    case AppIcon.Discover:
                        graphics.FillPolygon(brush, new PointF[] { new PointF(12, 2), new PointF(14, 9), new PointF(22, 12), new PointF(14, 15), new PointF(12, 22), new PointF(10, 15), new PointF(2, 12), new PointF(10, 9) }); graphics.FillEllipse(brush, 11, 11, 2, 2); break;
                    case AppIcon.Search:
                        graphics.DrawEllipse(pen, 4, 4, 11, 11); graphics.DrawLine(pen, 14, 14, 21, 21); break;
                    case AppIcon.Library:
                        graphics.DrawRectangle(pen, 4, 4, 4, 16); graphics.DrawRectangle(pen, 10, 4, 4, 16); graphics.DrawLine(pen, 17, 5, 20, 19); break;
                    case AppIcon.Playlist:
                        graphics.DrawLine(pen, 4, 6, 15, 6); graphics.DrawLine(pen, 4, 11, 15, 11); graphics.DrawLine(pen, 4, 16, 11, 16); graphics.FillPolygon(brush, new PointF[] { new PointF(15, 14), new PointF(22, 18), new PointF(15, 22) }); break;
                    case AppIcon.Queue:
                        graphics.DrawLine(pen, 3, 6, 15, 6); graphics.DrawLine(pen, 3, 12, 15, 12); graphics.DrawLine(pen, 3, 18, 11, 18); graphics.FillPolygon(brush, new PointF[] { new PointF(16, 15), new PointF(22, 18.5f), new PointF(16, 22) }); break;
                    case AppIcon.Settings:
                        graphics.DrawEllipse(pen, 8.5f, 8.5f, 7, 7); graphics.DrawEllipse(pen, 5, 5, 14, 14); graphics.DrawLine(pen, 12, 2, 12, 5); graphics.DrawLine(pen, 12, 19, 12, 22); graphics.DrawLine(pen, 2, 12, 5, 12); graphics.DrawLine(pen, 19, 12, 22, 12); graphics.DrawLine(pen, 5, 5, 7, 7); graphics.DrawLine(pen, 17, 17, 19, 19); graphics.DrawLine(pen, 19, 5, 17, 7); graphics.DrawLine(pen, 7, 17, 5, 19); break;
                    case AppIcon.Play:
                        graphics.FillPolygon(brush, new PointF[] { new PointF(8, 5), new PointF(20, 12), new PointF(8, 19) }); break;
                    case AppIcon.Pause:
                        graphics.FillRectangle(brush, 7, 5, 4, 14); graphics.FillRectangle(brush, 14, 5, 4, 14); break;
                    case AppIcon.Previous:
                        graphics.FillRectangle(brush, 5, 5, 2, 14); graphics.FillPolygon(brush, new PointF[] { new PointF(18, 5), new PointF(8, 12), new PointF(18, 19) }); break;
                    case AppIcon.Next:
                        graphics.FillRectangle(brush, 17, 5, 2, 14); graphics.FillPolygon(brush, new PointF[] { new PointF(6, 5), new PointF(16, 12), new PointF(6, 19) }); break;
                    case AppIcon.Stop:
                        graphics.FillRectangle(brush, 6, 6, 12, 12); break;
                    case AppIcon.Add:
                        graphics.DrawLine(pen, 12, 4, 12, 20); graphics.DrawLine(pen, 4, 12, 20, 12); break;
                    case AppIcon.Heart:
                        using (GraphicsPath heart = new GraphicsPath()) { heart.StartFigure(); heart.AddBezier(12, 21, 10, 18, 4, 15, 4, 9); heart.AddBezier(4, 9, 4, 4, 10, 3, 12, 7); heart.AddBezier(12, 7, 14, 3, 20, 4, 20, 9); heart.AddBezier(20, 9, 20, 15, 14, 18, 12, 21); heart.CloseFigure(); graphics.DrawPath(pen, heart); } break;
                    case AppIcon.More:
                        graphics.FillEllipse(brush, 4, 10, 4, 4); graphics.FillEllipse(brush, 10, 10, 4, 4); graphics.FillEllipse(brush, 16, 10, 4, 4); break;
                    case AppIcon.Volume:
                        graphics.FillPolygon(brush, new PointF[] { new PointF(3, 9), new PointF(8, 9), new PointF(13, 5), new PointF(13, 19), new PointF(8, 15), new PointF(3, 15) }); graphics.DrawArc(pen, 14, 8, 5, 8, -60, 120); graphics.DrawArc(pen, 13, 5, 9, 14, -55, 110); break;
                    case AppIcon.VolumeMuted:
                        graphics.FillPolygon(brush, new PointF[] { new PointF(3, 9), new PointF(8, 9), new PointF(13, 5), new PointF(13, 19), new PointF(8, 15), new PointF(3, 15) }); graphics.DrawLine(pen, 16, 9, 21, 15); graphics.DrawLine(pen, 21, 9, 16, 15); break;
                    case AppIcon.Shuffle:
                        graphics.DrawBezier(pen, 3, 7, 8, 7, 12, 17, 18, 17); graphics.DrawLine(pen, 18, 17, 21, 17); graphics.DrawLines(pen, new PointF[] { new PointF(18, 14), new PointF(21, 17), new PointF(18, 20) }); graphics.DrawBezier(pen, 3, 17, 8, 17, 12, 7, 18, 7); graphics.DrawLine(pen, 18, 7, 21, 7); graphics.DrawLines(pen, new PointF[] { new PointF(18, 4), new PointF(21, 7), new PointF(18, 10) }); break;
                    case AppIcon.Repeat:
                        graphics.DrawBezier(pen, 4, 10, 4, 7, 7, 5, 10, 5); graphics.DrawLine(pen, 10, 5, 21, 5); graphics.DrawLines(pen, new PointF[] { new PointF(18, 2), new PointF(21, 5), new PointF(18, 8) }); graphics.DrawBezier(pen, 20, 14, 20, 17, 17, 19, 14, 19); graphics.DrawLine(pen, 14, 19, 3, 19); graphics.DrawLines(pen, new PointF[] { new PointF(6, 16), new PointF(3, 19), new PointF(6, 22) }); break;
                    case AppIcon.RepeatOne:
                        graphics.DrawBezier(pen, 4, 10, 4, 7, 7, 5, 10, 5); graphics.DrawLine(pen, 10, 5, 21, 5); graphics.DrawLines(pen, new PointF[] { new PointF(18, 2), new PointF(21, 5), new PointF(18, 8) }); graphics.DrawBezier(pen, 20, 14, 20, 17, 17, 19, 14, 19); graphics.DrawLine(pen, 14, 19, 3, 19); graphics.DrawLines(pen, new PointF[] { new PointF(6, 16), new PointF(3, 19), new PointF(6, 22) }); using (Font one = new Font("Segoe UI", 7, FontStyle.Bold)) using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) graphics.DrawString("1", one, brush, new RectangleF(8, 8, 8, 8), format); break;
                    case AppIcon.Mini:
                        graphics.DrawRectangle(pen, 3, 4, 18, 16); graphics.FillRectangle(brush, 12, 13, 7, 5); break;
                    case AppIcon.Close:
                        graphics.DrawLine(pen, 5, 5, 19, 19); graphics.DrawLine(pen, 19, 5, 5, 19); break;
                    case AppIcon.Back:
                        graphics.DrawLines(pen, new PointF[] { new PointF(14, 5), new PointF(7, 12), new PointF(14, 19) }); break;
                    case AppIcon.Forward:
                        graphics.DrawLines(pen, new PointF[] { new PointF(10, 5), new PointF(17, 12), new PointF(10, 19) }); break;
                    case AppIcon.Download:
                        graphics.DrawLine(pen, 12, 3, 12, 16); graphics.DrawLines(pen, new PointF[] { new PointF(7, 11), new PointF(12, 16), new PointF(17, 11) }); graphics.DrawLine(pen, 4, 21, 20, 21); break;
                    case AppIcon.Check:
                        graphics.DrawLines(pen, new PointF[] { new PointF(4, 12), new PointF(10, 18), new PointF(21, 6) }); break;
                }
            }
            graphics.Restore(state);
        }
    }

    internal sealed class IconButton : Control
    {
        private bool hovered;
        private bool pressed;
        public AppIcon Icon { get; set; }
        public bool Accent { get; set; }
        public bool Checked { get; set; }

        public IconButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(38, 38);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            Color background = Accent ? (hovered ? Theme.AccentHover : Theme.Accent) : (hovered || Focused || Checked ? Theme.SurfaceHover : Color.Transparent);
            if (pressed) background = Theme.SurfaceSelected;
            if (background.A > 0) using (SolidBrush brush = new SolidBrush(background)) using (GraphicsPath path = Theme.Rounded(new Rectangle(1, 1, Width - 2, Height - 2), Height / 2)) e.Graphics.FillPath(brush, path);
            Color foreground = Accent ? Color.White : (Enabled ? Theme.Text : Theme.Faint);
            IconPainter.Draw(e.Graphics, Icon, new Rectangle((Width - 22) / 2, (Height - 22) / 2, 22, 22), foreground, 1.8f);
        }
    }

    internal sealed class NavButton : Control
    {
        private bool hovered;
        public AppIcon Icon { get; set; }
        public bool Selected { get; set; }
        public string Label { get; set; }

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Height = 46;
            Dock = DockStyle.Top;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            Rectangle box = new Rectangle(8, 3, Width - 16, Height - 6);
            if (Selected || hovered || Focused) using (SolidBrush brush = new SolidBrush(Selected ? Theme.SurfaceSelected : Theme.SurfaceHover)) using (GraphicsPath path = Theme.Rounded(box, 10)) e.Graphics.FillPath(brush, path);
            if (Selected) using (SolidBrush accent = new SolidBrush(Theme.Accent)) using (GraphicsPath marker = Theme.Rounded(new Rectangle(8, 12, 3, 22), 2)) e.Graphics.FillPath(accent, marker);
            IconPainter.Draw(e.Graphics, Icon, new Rectangle(22, 13, 20, 20), Selected ? Theme.Text : Theme.Muted, 1.8f);
            using (Font font = new Font("Segoe UI", 9.5f, Selected ? FontStyle.Bold : FontStyle.Regular)) TextRenderer.DrawText(e.Graphics, Label ?? "", font, new Rectangle(54, 0, Width - 64, Height), Selected ? Theme.Text : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class PillButton : Control
    {
        private bool hovered;
        public string Label { get; set; }
        public AppIcon Icon { get; set; }
        public bool ShowIcon { get; set; }
        public bool Primary { get; set; }

        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Height = 38;
            Width = 118;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            Color background = Primary ? (hovered ? Theme.AccentHover : Theme.Accent) : (hovered ? Theme.SurfaceSelected : Theme.SurfaceHover);
            using (SolidBrush brush = new SolidBrush(background)) using (GraphicsPath path = Theme.Rounded(new Rectangle(1, 1, Width - 2, Height - 2), 10)) e.Graphics.FillPath(brush, path);
            int left = 12;
            if (ShowIcon) { IconPainter.Draw(e.Graphics, Icon, new Rectangle(10, 9, 20, 20), Theme.Text, 1.8f); left = 38; }
            using (Font font = new Font("Segoe UI", 9, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, Label ?? "", font, new Rectangle(left, 0, Width - left - 8, Height), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class SectionCard : Panel
    {
        public SectionCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            Padding = new Padding(18);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            using (SolidBrush brush = new SolidBrush(Theme.Surface)) using (GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 14)) e.Graphics.FillPath(brush, path);
        }
    }
}
