using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace YTMusicLite.Client
{
    internal static class Branding
    {
        public static void DrawMark(Graphics graphics, RectangleF bounds)
        {
            Theme.EnableQuality(graphics);
            float size = Math.Min(bounds.Width, bounds.Height);
            Rectangle tile = new Rectangle((int)(bounds.X + (bounds.Width - size) / 2), (int)(bounds.Y + (bounds.Height - size) / 2), (int)size, (int)size);
            using (GraphicsPath path = Theme.Rounded(tile, Math.Max(3, tile.Width / 4)))
            using (SolidBrush brush = new SolidBrush(Theme.Accent)) graphics.FillPath(brush, path);
            float left = tile.Left + tile.Width * .39f;
            float top = tile.Top + tile.Height * .29f;
            using (SolidBrush white = new SolidBrush(Color.White)) graphics.FillPolygon(white, new PointF[] { new PointF(left, top), new PointF(tile.Left + tile.Width * .75f, tile.Top + tile.Height * .5f), new PointF(left, tile.Top + tile.Height * .71f) });
        }

        public static Bitmap CreateBitmap(int size)
        {
            Bitmap image = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(image)) { graphics.Clear(Color.Transparent); DrawMark(graphics, new RectangleF(0, 0, size, size)); }
            return image;
        }

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
        public static Icon CreateIcon()
        {
            using (Bitmap image = CreateBitmap(32))
            {
                IntPtr handle = image.GetHicon();
                try { using (Icon temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}
