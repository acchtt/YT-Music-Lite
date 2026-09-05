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

    public sealed class ToggleSwitch : Control
    {
        private bool isChecked;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            Size = new Size(40, 22);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 2, Width - 1, Height - 5);
            int radius = track.Height;
            using (GraphicsPath path = RoundedRect(track, radius))
            using (SolidBrush trackBrush = new SolidBrush(Checked ? Color.FromArgb(230, 45, 65) : Color.FromArgb(66, 66, 66)))
            {
                g.FillPath(trackBrush, path);
            }

            int knobSize = track.Height - 4;
            int knobX = Checked ? track.Right - knobSize - 2 : track.Left + 2;
            using (SolidBrush knobBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(knobBrush, knobX, track.Top + 2, knobSize, knobSize);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int diameter = Math.Max(2, radius);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 90, 180);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 180);
            path.CloseFigure();
            return path;
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

    public sealed class LiteMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 24, 24); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 24, 24); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 24, 24); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 24, 24); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(43, 43, 43); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(43, 43, 43); } }
        public override Color MenuBorder { get { return Color.FromArgb(52, 52, 52); } }
        public override Color SeparatorDark { get { return Color.FromArgb(54, 54, 54); } }
        public override Color SeparatorLight { get { return Color.FromArgb(54, 54, 54); } }
        public override Color CheckBackground { get { return Color.FromArgb(230, 45, 65); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(230, 45, 65); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(230, 45, 65); } }
    }

    public sealed class LiteMenuRenderer : ToolStripProfessionalRenderer
    {
        public LiteMenuRenderer() : base(new LiteMenuColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(235, 235, 235);
            base.OnRenderItemText(e);
        }
    }
}
