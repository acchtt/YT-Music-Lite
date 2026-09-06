using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YTMusicLite
{
    public static class BrandArt
    {
        public static readonly Color Accent = Color.FromArgb(255, 76, 103);
        public static readonly Color Surface = Color.FromArgb(22, 23, 29);
        public static readonly Color Muted = Color.FromArgb(157, 160, 174);

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // A single musical note whose flag is a play triangle. The same geometry
        // powers the wordmark, artwork fallback, tray and executable icon.
        public static void DrawMark(Graphics graphics, RectangleF bounds)
        {
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 64f, bounds.Height / 64f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath tile = Rounded(new RectangleF(1, 1, 62, 62), 18))
            using (LinearGradientBrush coral = new LinearGradientBrush(new Point(8, 0), new Point(55, 64), Color.FromArgb(255, 103, 117), Color.FromArgb(230, 37, 79)))
                graphics.FillPath(coral, tile);
            using (GraphicsPath note = new GraphicsPath())
            {
                note.AddEllipse(16, 37, 22, 15);
                note.AddRectangle(new RectangleF(31, 16, 7, 29));
                note.AddPolygon(new PointF[] { new PointF(36, 15), new PointF(51, 25), new PointF(36, 35) });
                note.FillMode = FillMode.Winding;
                graphics.FillPath(Brushes.White, note);
            }
            graphics.Restore(state);
        }

        public static Bitmap CreateBitmap(int size)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                DrawMark(g, new RectangleF(0, 0, size, size));
            }
            return bitmap;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
        public static Icon CreateIcon()
        {
            using (Bitmap bitmap = CreateBitmap(32))
            {
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }

    public sealed class BrandLogoControl : Control
    {
        public bool MarkOnly { get; set; }
        public BrandLogoControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            TabStop = false;
            AccessibleName = "YT Music Lite";
            Size = new Size(150, 38);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float size = Math.Min(Height - 4, MarkOnly ? Width - 4 : Width * 0.24f);
            BrandArt.DrawMark(e.Graphics, new RectangleF(2, (Height - size) / 2, size, size));
            if (MarkOnly) return;
            float scale = Height / 38f;
            using (Font name = new Font("Segoe UI", 10f * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font lite = new Font("Segoe UI", 9f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                int left = (int)(size + 10);
                TextRenderer.DrawText(e.Graphics, "YT MUSIC", name, new Rectangle(left, (int)(Height * 0.18f), Width - left, (int)(Height * 0.38f)), Color.FromArgb(242, 243, 248), TextFormatFlags.Left | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, "L I T E", lite, new Rectangle(left, (int)(Height * 0.57f), Width - left, (int)(Height * 0.33f)), BrandArt.Muted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }
}
