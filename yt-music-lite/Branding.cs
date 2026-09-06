using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class BrandLogoControl : Control
    {
        public BrandLogoControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            TabStop = false;
            Size = new Size(150, 38);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.ScaleTransform(Width / 150f, Height / 38f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle icon = new Rectangle(1, 3, 34, 32);
            using (GraphicsPath outer = RoundedRect(icon, 9))
            using (LinearGradientBrush red = new LinearGradientBrush(icon, Color.FromArgb(255, 48, 70), Color.FromArgb(174, 18, 38), 55f))
            {
                g.FillPath(red, outer);
            }

            Rectangle innerRect = new Rectangle(4, 6, 28, 26);
            using (GraphicsPath inner = RoundedRect(innerRect, 7))
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(19, 19, 22)))
            {
                g.FillPath(dark, inner);
            }

            using (Pen wave = new Pen(Color.FromArgb(255, 48, 70), 3f))
            {
                wave.StartCap = LineCap.Round;
                wave.EndCap = LineCap.Round;
                g.DrawLine(wave, 8, 19, 8, 23);
                g.DrawLine(wave, 13, 16, 13, 26);
                g.DrawLine(wave, 18, 12, 18, 30);
            }

            PointF[] play = new PointF[]
            {
                new PointF(22, 13),
                new PointF(22, 27),
                new PointF(31, 20)
            };
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillPolygon(white, play);
            }

            using (Font bold = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point))
            using (Font regular = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point))
            using (SolidBrush redText = new SolidBrush(Color.FromArgb(255, 58, 78)))
            using (SolidBrush whiteText = new SolidBrush(Color.FromArgb(242, 242, 242)))
            using (SolidBrush liteText = new SolidBrush(Color.FromArgb(155, 155, 155)))
            {
                float x = 43f;
                float y = 9f;
                g.DrawString("YT", bold, redText, x, y);
                x += g.MeasureString("YT", bold).Width - 1f;
                g.DrawString(" Music", bold, whiteText, x, y);
                x += g.MeasureString(" Music", bold).Width - 1f;
                g.DrawString(" Lite", regular, liteText, x, y);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
