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
        Info
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
        public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color, float stroke)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            float left = r.Left;
            float top = r.Top;
            float right = r.Right;
            float bottom = r.Bottom;
            float cx = left + r.Width / 2f;
            float cy = top + r.Height / 2f;
            using (Pen pen = new Pen(color, stroke))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (kind == IconKind.Back || kind == IconKind.Forward)
                {
                    float dir = kind == IconKind.Back ? -1f : 1f;
                    float tip = cx + dir * 5f;
                    float tail = cx - dir * 5f;
                    g.DrawLine(pen, tail, cy - 6f, tip, cy);
                    g.DrawLine(pen, tip, cy, tail, cy + 6f);
                    return;
                }

                if (kind == IconKind.Reload || kind == IconKind.Update)
                {
                    RectangleF arc = new RectangleF(cx - 7f, cy - 7f, 14f, 14f);
                    g.DrawArc(pen, arc, -55f, 280f);
                    PointF p1 = new PointF(cx + 6.3f, cy - 6.1f);
                    PointF p2 = new PointF(cx + 6.7f, cy - 1.5f);
                    PointF p3 = new PointF(cx + 2.4f, cy - 4.2f);
                    g.FillPolygon(brush, new PointF[] { p1, p2, p3 });
                    if (kind == IconKind.Update)
                    {
                        g.DrawLine(pen, cx, cy - 3f, cx, cy + 4f);
                        g.DrawLine(pen, cx - 3f, cy + 1f, cx, cy + 4f);
                        g.DrawLine(pen, cx, cy + 4f, cx + 3f, cy + 1f);
                    }
                    return;
                }

                if (kind == IconKind.Home)
                {
                    g.DrawLine(pen, cx - 7f, cy - 1f, cx, cy - 7f);
                    g.DrawLine(pen, cx, cy - 7f, cx + 7f, cy - 1f);
                    g.DrawLine(pen, cx - 5.5f, cy - 2f, cx - 5.5f, cy + 7f);
                    g.DrawLine(pen, cx + 5.5f, cy - 2f, cx + 5.5f, cy + 7f);
                    g.DrawLine(pen, cx - 5.5f, cy + 7f, cx + 5.5f, cy + 7f);
                    return;
                }

                if (kind == IconKind.MiniPlayer)
                {
                    g.DrawRoundedRectangle(pen, new RectangleF(cx - 8f, cy - 6f, 16f, 12f), 2.5f);
                    g.DrawLine(pen, cx + 1f, cy + 2f, cx + 6f, cy + 2f);
                    g.DrawLine(pen, cx + 6f, cy + 2f, cx + 6f, cy - 2f);
                    return;
                }

                if (kind == IconKind.Settings)
                {
                    g.DrawEllipse(pen, cx - 3f, cy - 3f, 6f, 6f);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = Math.PI * i / 4.0;
                        float x1 = cx + (float)Math.Cos(a) * 5.5f;
                        float y1 = cy + (float)Math.Sin(a) * 5.5f;
                        float x2 = cx + (float)Math.Cos(a) * 8f;
                        float y2 = cy + (float)Math.Sin(a) * 8f;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                    return;
                }

                if (kind == IconKind.Previous || kind == IconKind.Next)
                {
                    bool next = kind == IconKind.Next;
                    float barX = next ? cx + 6f : cx - 6f;
                    g.DrawLine(pen, barX, cy - 6f, barX, cy + 6f);
                    PointF a = new PointF(next ? cx - 5f : cx + 5f, cy - 6f);
                    PointF b = new PointF(next ? cx + 3f : cx - 3f, cy);
                    PointF c = new PointF(next ? cx - 5f : cx + 5f, cy + 6f);
                    g.FillPolygon(brush, new PointF[] { a, b, c });
                    return;
                }

                if (kind == IconKind.Play)
                {
                    g.FillPolygon(brush, new PointF[] {
                        new PointF(cx - 4f, cy - 7f),
                        new PointF(cx + 7f, cy),
                        new PointF(cx - 4f, cy + 7f)
                    });
                    return;
                }

                if (kind == IconKind.Pause)
                {
                    g.FillRectangle(brush, cx - 5f, cy - 7f, 3.5f, 14f);
                    g.FillRectangle(brush, cx + 1.5f, cy - 7f, 3.5f, 14f);
                    return;
                }

                if (kind == IconKind.Volume)
                {
                    PointF[] speaker = new PointF[] {
                        new PointF(cx - 8f, cy - 3f),
                        new PointF(cx - 4f, cy - 3f),
                        new PointF(cx + 1f, cy - 7f),
                        new PointF(cx + 1f, cy + 7f),
                        new PointF(cx - 4f, cy + 3f),
                        new PointF(cx - 8f, cy + 3f)
                    };
                    g.FillPolygon(brush, speaker);
                    g.DrawArc(pen, cx - 1f, cy - 5f, 10f, 10f, -55f, 110f);
                    g.DrawArc(pen, cx - 1f, cy - 8f, 16f, 16f, -50f, 100f);
                    return;
                }

                if (kind == IconKind.Close)
                {
                    g.DrawLine(pen, cx - 5f, cy - 5f, cx + 5f, cy + 5f);
                    g.DrawLine(pen, cx + 5f, cy - 5f, cx - 5f, cy + 5f);
                    return;
                }

                if (kind == IconKind.Window)
                {
                    g.DrawRoundedRectangle(pen, new RectangleF(cx - 7f, cy - 6f, 14f, 12f), 2f);
                    g.DrawLine(pen, cx - 4f, cy - 3f, cx + 4f, cy - 3f);
                    return;
                }

                if (kind == IconKind.Sleep)
                {
                    g.DrawArc(pen, cx - 7f, cy - 8f, 14f, 16f, 70f, 220f);
                    g.DrawArc(pen, cx - 1f, cy - 8f, 10f, 14f, 100f, 160f);
                    return;
                }

                if (kind == IconKind.General)
                {
                    g.DrawLine(pen, cx - 7f, cy - 5f, cx + 7f, cy - 5f);
                    g.DrawLine(pen, cx - 7f, cy, cx + 7f, cy);
                    g.DrawLine(pen, cx - 7f, cy + 5f, cx + 7f, cy + 5f);
                    g.FillEllipse(brush, cx - 4f, cy - 7f, 4f, 4f);
                    g.FillEllipse(brush, cx + 1f, cy - 2f, 4f, 4f);
                    g.FillEllipse(brush, cx - 2f, cy + 3f, 4f, 4f);
                    return;
                }

                if (kind == IconKind.Info)
                {
                    g.DrawEllipse(pen, cx - 7f, cy - 7f, 14f, 14f);
                    g.FillEllipse(brush, cx - 1.3f, cy - 4.5f, 2.6f, 2.6f);
                    g.DrawLine(pen, cx, cy, cx, cy + 5f);
                }
            }
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
            using (Pen foreground = new Pen(interactive && Enabled ? Color.FromArgb(238, 47, 67) : Color.FromArgb(100, 100, 100), 3f))
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
                using (SolidBrush brush = new SolidBrush(Enabled && Checked ? Color.FromArgb(238, 47, 67) : Color.FromArgb(80, 80, 80))) g.FillPath(brush, path);
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
        public IconKind? DrawnIcon { get { return drawnIcon; } set { drawnIcon = value; Invalidate(); } }
        public IconButtonStyle IconStyle { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!drawnIcon.HasValue) { base.OnPaint(e); return; }
            bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color background = IconStyle == IconButtonStyle.Light ? Color.FromArgb(245, 245, 245) : hover && Enabled ? Color.FromArgb(40, 40, 40) : Parent.BackColor;
            e.Graphics.Clear(background);
            Color foreground = !Enabled ? Color.FromArgb(95, 95, 95) : IconStyle == IconButtonStyle.Light ? Color.FromArgb(18, 18, 18) : Color.FromArgb(220, 220, 220);
            int size = Math.Min(20, Math.Min(Width, Height) - 10);
            IconArt.Draw(e.Graphics, drawnIcon.Value, new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), foreground, 1.7f);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), foreground, background);
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
        public override Color CheckBackground { get { return Color.FromArgb(238, 47, 67); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(238, 47, 67); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(238, 47, 67); } }
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
