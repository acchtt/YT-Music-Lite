using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YTMusicLite
{
    public enum IconKind
    {
        Back,
        Forward,
        Reload,
        Home,
        MiniPlayer,
        Settings,
        Previous,
        Play,
        Pause,
        Next,
        Volume,
        Close,
        Window,
        Sleep,
        Update,
        General,
        Info,
        Pin,
        Unpin,
        Muted
    }

    public enum IconButtonStyle
    {
        Ghost,
        Soft,
        Light,
        Accent,
        Danger
    }

    public static class IconArt
    {
        // All glyphs share a 24-unit grid, rounded terminals, and optical padding.
        public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color, float stroke)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, 1.8f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (kind)
                {
                    case IconKind.Back:
                    case IconKind.Forward:
                        bool back = kind == IconKind.Back;
                        g.DrawLines(pen, new PointF[] { new PointF(back ? 14 : 10, 5), new PointF(back ? 7 : 17, 12), new PointF(back ? 14 : 10, 19) });
                        break;
                    case IconKind.Reload:
                        g.DrawArc(pen, 4, 4, 16, 16, 35, 290);
                        g.DrawLines(pen, new PointF[] { new PointF(20, 3), new PointF(20, 8), new PointF(15, 8) });
                        break;
                    case IconKind.Home:
                        g.DrawLines(pen, new PointF[] { new PointF(3, 10), new PointF(12, 3), new PointF(21, 10) });
                        g.DrawLines(pen, new PointF[] { new PointF(5, 9), new PointF(5, 21), new PointF(10, 21), new PointF(10, 15), new PointF(14, 15), new PointF(14, 21), new PointF(19, 21), new PointF(19, 9) });
                        break;
                    case IconKind.MiniPlayer:
                        using (GraphicsPath frame = BrandArt.Rounded(new RectangleF(2, 4, 20, 16), 3)) g.DrawPath(pen, frame);
                        using (GraphicsPath inset = BrandArt.Rounded(new RectangleF(12, 11, 7, 6), 1.5f)) g.FillPath(brush, inset);
                        break;
                    case IconKind.Window:
                        g.DrawLines(pen, new PointF[] { new PointF(14, 3), new PointF(21, 3), new PointF(21, 10) });
                        g.DrawLine(pen, 21, 3, 12, 12);
                        g.DrawLines(pen, new PointF[] { new PointF(10, 4), new PointF(4, 4), new PointF(4, 20), new PointF(20, 20), new PointF(20, 14) });
                        break;
                    case IconKind.Settings:
                        PointF[] gear = new PointF[32];
                        for (int i = 0; i < gear.Length; i++)
                        {
                            double angle = i * Math.PI / 16;
                            float radius = i % 4 == 0 || i % 4 == 3 ? 9.5f : 7.5f;
                            gear[i] = new PointF(12 + (float)Math.Cos(angle) * radius, 12 + (float)Math.Sin(angle) * radius);
                        }
                        g.DrawPolygon(pen, gear);
                        g.DrawEllipse(pen, 9, 9, 6, 6);
                        break;
                    case IconKind.Previous:
                    case IconKind.Next:
                        bool next = kind == IconKind.Next;
                        g.DrawLine(pen, next ? 19 : 5, 5, next ? 19 : 5, 19);
                        g.FillPolygon(brush, new PointF[] { new PointF(next ? 5 : 19, 5), new PointF(next ? 15 : 9, 12), new PointF(next ? 5 : 19, 19) });
                        break;
                    case IconKind.Play:
                        using (GraphicsPath play = new GraphicsPath())
                        {
                            play.AddLines(new PointF[] { new PointF(7, 4), new PointF(20, 12), new PointF(7, 20) });
                            play.CloseFigure();
                            g.FillPath(brush, play);
                        }
                        break;
                    case IconKind.Pause:
                        using (GraphicsPath left = BrandArt.Rounded(new RectangleF(6, 4, 4, 16), 1.5f)) g.FillPath(brush, left);
                        using (GraphicsPath right = BrandArt.Rounded(new RectangleF(14, 4, 4, 16), 1.5f)) g.FillPath(brush, right);
                        break;
                    case IconKind.Volume:
                    case IconKind.Muted:
                        g.DrawPolygon(pen, new PointF[] { new PointF(3, 9), new PointF(7, 9), new PointF(12, 5), new PointF(12, 19), new PointF(7, 15), new PointF(3, 15) });
                        if (kind == IconKind.Volume)
                        {
                            g.DrawArc(pen, 11, 7, 8, 10, -65, 130);
                            g.DrawArc(pen, 10, 3, 13, 18, -60, 120);
                        }
                        else { g.DrawLine(pen, 17, 9, 22, 15); g.DrawLine(pen, 22, 9, 17, 15); }
                        break;
                    case IconKind.Close:
                        g.DrawLine(pen, 6, 6, 18, 18); g.DrawLine(pen, 18, 6, 6, 18);
                        break;
                    case IconKind.Sleep:
                        using (GraphicsPath moon = new GraphicsPath())
                        {
                            moon.AddBezier(19, 15, 11, 18, 5, 10, 10, 3);
                            moon.AddBezier(10, 3, -1, 5, 1, 21, 12, 21);
                            moon.AddBezier(12, 21, 16, 21, 18, 18, 19, 15);
                            g.DrawPath(pen, moon);
                        }
                        break;
                    case IconKind.Update:
                        g.DrawLine(pen, 12, 3, 12, 15);
                        g.DrawLines(pen, new PointF[] { new PointF(7, 10), new PointF(12, 15), new PointF(17, 10) });
                        g.DrawLines(pen, new PointF[] { new PointF(4, 16), new PointF(4, 21), new PointF(20, 21), new PointF(20, 16) });
                        break;
                    case IconKind.Pin:
                    case IconKind.Unpin:
                        g.DrawLines(pen, new PointF[] { new PointF(8, 3), new PointF(16, 3), new PointF(16, 9), new PointF(19, 13), new PointF(5, 13), new PointF(8, 9), new PointF(8, 3) });
                        g.DrawLine(pen, 12, 13, 12, 21);
                        if (kind == IconKind.Unpin) g.DrawLine(pen, 3, 3, 21, 21);
                        break;
                    case IconKind.General:
                        for (int i = 0; i < 3; i++)
                        {
                            int x = 5 + i * 7, y = i == 1 ? 15 : 8;
                            g.DrawLine(pen, x, 3, x, y - 3); g.DrawLine(pen, x, y + 3, x, 21);
                            g.DrawEllipse(pen, x - 3, y - 3, 6, 6);
                        }
                        break;
                    case IconKind.Info:
                        g.DrawEllipse(pen, 3, 3, 18, 18);
                        g.FillEllipse(brush, 11, 6, 2, 2);
                        g.DrawLine(pen, 12, 11, 12, 17);
                        break;
                }
            }
            g.Restore(state);
        }

        public static Bitmap CreateBitmap(IconKind kind, int size, Color color)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                Draw(g, kind, new Rectangle(2, 2, size - 4, size - 4), color, Math.Max(1.4f, size / 12f));
            }
            return bitmap;
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        private static void DrawRoundedRectangle(this Graphics g, Pen pen, RectangleF rect, float radius)
        {
            using (GraphicsPath path = RoundedRect(rect, radius)) g.DrawPath(pen, path);
        }
    }

    public sealed class IconButton : Button
    {
        private bool hovered;
        private bool pressed;
        private IconKind icon;
        private IconButtonStyle buttonStyle;

        public IconButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(34, 34);
            Cursor = Cursors.Hand;
            TabStop = true;
            ForeColor = Color.FromArgb(222, 222, 222);
            icon = IconKind.Play;
            buttonStyle = IconButtonStyle.Ghost;
        }

        public IconKind Icon
        {
            get { return icon; }
            set { icon = value; Invalidate(); }
        }

        public IconButtonStyle ButtonStyle
        {
            get { return buttonStyle; }
            set { buttonStyle = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Color background = Color.Transparent;
            Color iconColor = Enabled ? ForeColor : Color.FromArgb(95, 95, 95);
            bool fill = false;

            if (buttonStyle == IconButtonStyle.Light)
            {
                fill = true;
                background = pressed ? Color.FromArgb(210, 210, 210) : hovered ? Color.FromArgb(232, 232, 232) : Color.FromArgb(248, 248, 248);
                iconColor = Color.FromArgb(18, 18, 18);
            }
            else if (buttonStyle == IconButtonStyle.Accent)
            {
                fill = true;
                background = pressed ? Color.FromArgb(205, 38, 57) : hovered ? Color.FromArgb(248, 58, 78) : Color.FromArgb(235, 47, 67);
                iconColor = Color.White;
            }
            else if (buttonStyle == IconButtonStyle.Soft)
            {
                fill = true;
                background = pressed ? Color.FromArgb(49, 49, 49) : hovered ? Color.FromArgb(42, 42, 42) : Color.FromArgb(29, 29, 29);
            }
            else if (buttonStyle == IconButtonStyle.Danger)
            {
                if (hovered || pressed)
                {
                    fill = true;
                    background = pressed ? Color.FromArgb(104, 31, 40) : Color.FromArgb(72, 31, 37);
                }
                iconColor = hovered ? Color.FromArgb(255, 190, 196) : ForeColor;
            }
            else if (hovered || pressed)
            {
                fill = true;
                background = pressed ? Color.FromArgb(44, 44, 44) : Color.FromArgb(34, 34, 34);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (fill)
            {
                Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);
                using (GraphicsPath path = RoundedRect(rect, Math.Min(9, rect.Height / 2)))
                using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillPath(brush, path);
            }

            int iconSize = Math.Max(16, Math.Min(20, Math.Min(Width, Height) - 12));
            Rectangle iconBounds = new Rectangle((Width - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize);
            IconArt.Draw(e.Graphics, icon, iconBounds, Enabled ? iconColor : Color.FromArgb(95, 95, 95), 1.7f);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), ForeColor, BackColor);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class IconGlyph : Control
    {
        private IconKind icon;

        public IconGlyph()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.FromArgb(165, 165, 165);
            Size = new Size(22, 22);
            TabStop = false;
        }

        public IconKind Icon
        {
            get { return icon; }
            set { icon = value; Invalidate(); }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent != null) e.Graphics.Clear(Parent.BackColor);
            else base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int size = Math.Min(18, Math.Min(Width, Height));
            IconArt.Draw(e.Graphics, icon, new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), ForeColor, 1.55f);
        }
    }

    public sealed class IconTextButton : Button
    {
        private bool hovered;
        private bool pressed;
        private IconKind icon;
        private IconButtonStyle buttonStyle;

        public IconTextButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(110, 32);
            Cursor = Cursors.Hand;
            TabStop = true;
            ForeColor = Color.FromArgb(232, 232, 232);
            Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);
            icon = IconKind.Window;
            buttonStyle = IconButtonStyle.Soft;
        }

        public IconKind Icon { get { return icon; } set { icon = value; Invalidate(); } }
        public IconButtonStyle ButtonStyle { get { return buttonStyle; } set { buttonStyle = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Color bg = pressed ? Color.FromArgb(48, 48, 48) : hovered ? Color.FromArgb(40, 40, 40) : Color.FromArgb(28, 28, 28);
            Color iconColor = Color.FromArgb(180, 180, 180);
            Color textColor = Enabled ? ForeColor : Color.FromArgb(95, 95, 95);
            if (buttonStyle == IconButtonStyle.Accent)
            {
                bg = pressed ? Color.FromArgb(205, 38, 57) : hovered ? Color.FromArgb(248, 58, 78) : Color.FromArgb(235, 47, 67);
                iconColor = Color.White;
                textColor = Color.White;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, 7))
            using (SolidBrush brush = new SolidBrush(bg)) e.Graphics.FillPath(brush, path);

            Rectangle iconBounds = new Rectangle(10, (Height - 16) / 2, 16, 16);
            IconArt.Draw(e.Graphics, icon, iconBounds, iconColor, 1.45f);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(34, 0, Width - 42, Height), textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), ForeColor, BackColor);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class SeekBar : Control
    {
        private double ratio;
        private bool interactive = true;
        private bool dragging;
        private readonly ToolTip preview = new ToolTip();
        public event EventHandler SeekRequested;
        public double Duration { get; set; }
        public double KeyboardStep { get; set; }
        public bool IsDragging { get { return dragging; } }

        public SeekBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            Height = 24;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.Slider;
            KeyboardStep = 0.05;
        }

        public double Ratio
        {
            get { return ratio; }
            set { if (!dragging) SetRatio(value); }
        }

        private void SetRatio(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            ratio = Math.Max(0, Math.Min(1, value));
            Invalidate();
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        public bool Interactive
        {
            get { return interactive; }
            set
            {
                interactive = value;
                TabStop = value;
                Cursor = value ? Cursors.Hand : Cursors.Default;
                if (!value) { dragging = false; Capture = false; }
                Invalidate();
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Home || key == Keys.End || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!interactive || !Enabled) return;
            double step = Duration > 0 ? 5 / Duration : KeyboardStep;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) SetRatio(ratio - step);
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) SetRatio(ratio + step);
            else if (e.KeyCode == Keys.Home) SetRatio(0);
            else if (e.KeyCode == Keys.End) SetRatio(1);
            else return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            Commit();
        }

        private double RatioAt(int x) { return Math.Max(0, Math.Min(1, (x - 4) / (double)Math.Max(1, Width - 8))); }
        private void Commit() { if (SeekRequested != null) SeekRequested(this, EventArgs.Empty); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!interactive || !Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            Capture = true;
            SetRatio(RatioAt(e.X));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!interactive) return;
            double target = RatioAt(e.X);
            if (dragging) SetRatio(target);
            string value = Duration > 0 ? FormatTime(target * Duration) : Math.Round(target * 100) + "%";
            preview.SetToolTip(this, value);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging || e.Button != MouseButtons.Left) return;
            SetRatio(RatioAt(e.X));
            dragging = false;
            Capture = false;
            Commit();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { dragging = false; Invalidate(); }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) preview.Dispose(); base.Dispose(disposing); }

        public static string FormatTime(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) value = 0;
            TimeSpan time = TimeSpan.FromSeconds(Math.Min(value, int.MaxValue));
            return time.TotalHours >= 1 ? ((int)time.TotalHours) + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00") : ((int)time.TotalMinutes) + ":" + time.Seconds.ToString("00");
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new SliderAccessibleObject(this); }
        private sealed class SliderAccessibleObject : ControlAccessibleObject
        {
            private readonly SeekBar slider;
            public SliderAccessibleObject(SeekBar owner) : base(owner) { slider = owner; }
            public override string Value
            {
                get { return slider.Duration > 0 ? FormatTime(slider.Ratio * slider.Duration) + " of " + FormatTime(slider.Duration) : Math.Round(slider.Ratio * 100) + "%"; }
                set { }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent != null) e.Graphics.Clear(Parent.BackColor);
            else base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int y = Height / 2;
            int right = Math.Max(4, Width - 4);
            int fill = 4 + (int)Math.Round((right - 4) * ratio);
            using (Pen background = new Pen(Color.FromArgb(67, 67, 67), 3f))
            using (Pen foreground = new Pen(interactive && Enabled ? BrandArt.Accent : Color.FromArgb(100, 100, 100), 3f))
            {
                g.DrawLine(background, 4, y, right, y);
                if (fill > 4) g.DrawLine(foreground, 4, y, fill, y);
                if (interactive) g.FillEllipse(Brushes.White, fill - 4, y - 4, 8, 8);
            }
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -1), ForeColor, Parent == null ? Color.Black : Parent.BackColor);
        }
    }

    public sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(46, 28);
            AutoSize = false;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(3, 5, Width - 7, Height - 11);
            int diameter = track.Height;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(track.Left, track.Top, diameter, diameter, 90, 180);
                path.AddArc(track.Right - diameter, track.Top, diameter, diameter, 270, 180);
                path.CloseFigure();
                using (SolidBrush brush = new SolidBrush(Enabled && Checked ? BrandArt.Accent : Color.FromArgb(80, 80, 80))) g.FillPath(brush, path);
            }
            int knob = track.Height - 4;
            int x = Checked ? track.Right - knob - 2 : track.Left + 2;
            g.FillEllipse(Brushes.White, x, track.Top + 2, knob, knob);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -1), ForeColor, BackColor);
        }
    }

    public sealed class LiteButton : Button
    {
        private IconKind? drawnIcon;
        private bool pressed;
        private bool selected;
        public bool ShowIconCaption { get; set; }
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public IconKind? DrawnIcon { get { return drawnIcon; } set { drawnIcon = value; Invalidate(); } }
        public IconButtonStyle IconStyle { get; set; }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { pressed = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = true; base.OnKeyDown(e); Invalidate(); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; base.OnKeyUp(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!drawnIcon.HasValue) { base.OnPaint(e); return; }
            bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color surface = Parent == null ? BackColor : Parent.BackColor;
            e.Graphics.Clear(surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool primary = IconStyle == IconButtonStyle.Light || IconStyle == IconButtonStyle.Accent;
            Color fill = primary ? (pressed ? Color.FromArgb(223, 53, 83) : hover ? Color.FromArgb(255, 103, 124) : BrandArt.Accent) : Selected ? Color.FromArgb(63, 33, 45) : Color.FromArgb(38, 40, 49);
            if (primary || hover || Selected || pressed || IconStyle == IconButtonStyle.Soft)
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    if (primary) e.Graphics.FillEllipse(brush, 2, 2, Width - 4, Height - 4);
                    else using (GraphicsPath path = BrandArt.Rounded(new RectangleF(2, 2, Width - 4, Height - 4), 9)) e.Graphics.FillPath(brush, path);
                }
            }
            Color foreground = !Enabled ? Color.FromArgb(87, 90, 102) : Selected ? BrandArt.Accent : Color.FromArgb(239, 240, 247);
            int size = Math.Max(16, (int)(Math.Min(Width, Height) * (primary ? 0.44f : 0.66f)));
            int iconX = ShowIconCaption ? (int)(10 * e.Graphics.DpiX / 96f) : (Width - size) / 2;
            IconArt.Draw(e.Graphics, drawnIcon.Value, new Rectangle(iconX, (Height - size) / 2, size, size), foreground, 1.8f);
            if (ShowIconCaption)
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(iconX + size + 7, 0, Width - iconX - size - 12, Height), foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                using (Pen focus = new Pen(Color.FromArgb(230, 233, 246), 1.5f))
                using (GraphicsPath path = BrandArt.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), primary ? Width / 2 : 9)) e.Graphics.DrawPath(focus, path);
        }
        public LiteButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 47, 47);
            FlatAppearance.MouseDownBackColor = Color.FromArgb(58, 58, 58);
            BackColor = Color.FromArgb(31, 31, 31);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            TabStop = true;
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
        public override Color CheckBackground { get { return BrandArt.Accent; } }
        public override Color CheckSelectedBackground { get { return BrandArt.Accent; } }
        public override Color CheckPressedBackground { get { return BrandArt.Accent; } }
    }

    public sealed class LiteMenuRenderer : ToolStripProfessionalRenderer
    {
        public LiteMenuRenderer() : base(new LiteMenuColorTable()) { RoundedEdges = true; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(235, 235, 235);
            base.OnRenderItemText(e);
        }
    }
}
