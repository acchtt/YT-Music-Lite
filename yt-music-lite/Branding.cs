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

        // Three rounded sound bars: deliberately simple enough to read at 16 px.
        public static void DrawMark(Graphics graphics, RectangleF bounds)
        {
            GraphicsState state = graphics.Save();
            float size = Math.Min(bounds.Width, bounds.Height);
            graphics.TranslateTransform(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2);
            graphics.ScaleTransform(size / 64f, size / 64f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath tile = Rounded(new RectangleF(1, 1, 62, 62), 17))
            using (SolidBrush coral = new SolidBrush(Accent)) graphics.FillPath(coral, tile);
            using (GraphicsPath left = Rounded(new RectangleF(15, 25, 8, 17), 4))
            using (GraphicsPath middle = Rounded(new RectangleF(28, 16, 8, 32), 4))
            using (GraphicsPath right = Rounded(new RectangleF(41, 22, 8, 21), 4))
            {
                graphics.FillPath(Brushes.White, left);
                graphics.FillPath(Brushes.White, middle);
                graphics.FillPath(Brushes.White, right);
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
            float dpi = e.Graphics.DpiX / 96f;
            float size = MarkOnly ? Math.Min(Width - 4, Height - 4) : Math.Min(28 * dpi, Height - 4);
            BrandArt.DrawMark(e.Graphics, new RectangleF(0, (Height - size) / 2, size, size));
            if (MarkOnly) return;
            int left = (int)(size + 10 * dpi);
            using (Font name = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, "YT Music Lite", name, new Rectangle(left, 0, Width - left, Height), Color.FromArgb(235, 237, 243), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }
}
