using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite
{
    public sealed class SeekBar : Control
    {
        private double ratio;
        private bool interactive = true;

        public event EventHandler SeekRequested;

        public SeekBar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);
            UpdateStyles();

            Height = 18;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public double Ratio
        {
            get { return ratio; }
            set
            {
                double next = value;
                if (next < 0) next = 0;
                if (next > 1) next = 1;
                if (Math.Abs(ratio - next) < 0.0001) return;
                ratio = next;
                Invalidate();
            }
        }

        public bool Interactive
        {
            get { return interactive; }
            set
            {
                interactive = value;
                Cursor = value ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!interactive || Width <= 1) return;
            Ratio = e.X / (double)Math.Max(1, Width - 1);
            if (SeekRequested != null) SeekRequested(this, EventArgs.Empty);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            if (BackColor == Color.Transparent && Parent != null)
            {
                pevent.Graphics.Clear(Parent.BackColor);
                return;
            }
            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int y = Height / 2;
            int left = 3;
            int right = Math.Max(left, Width - 3);
            int fill = left + (int)Math.Round((right - left) * ratio);

            using (Pen backgroundPen = new Pen(Color.FromArgb(75, 75, 75), 3f))
            using (Pen fillPen = new Pen(Color.FromArgb(255, 60, 78), 3f))
            using (SolidBrush knob = new SolidBrush(Color.White))
            {
                backgroundPen.StartCap = LineCap.Round;
                backgroundPen.EndCap = LineCap.Round;
                fillPen.StartCap = LineCap.Round;
                fillPen.EndCap = LineCap.Round;
                g.DrawLine(backgroundPen, left, y, right, y);
                if (fill > left) g.DrawLine(fillPen, left, y, fill, y);
                g.FillEllipse(knob, fill - 4, y - 4, 8, 8);
            }
        }
    }

    public sealed class LiteButton : Button
    {
        public LiteButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 47, 47);
            FlatAppearance.MouseDownBackColor = Color.FromArgb(58, 58, 58);
            BackColor = Color.FromArgb(31, 31, 31);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            TabStop = false;
            Cursor = Cursors.Hand;
        }
    }
}
