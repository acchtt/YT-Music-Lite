using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal static class MusicTheme
{
    public static readonly Color Window = Color.FromArgb(10, 10, 10);
    public static readonly Color Sidebar = Color.FromArgb(0, 0, 0);
    public static readonly Color Canvas = Color.FromArgb(18, 18, 18);
    public static readonly Color Raised = Color.FromArgb(36, 36, 36);
    public static readonly Color Hover = Color.FromArgb(48, 48, 48);
    public static readonly Color Player = Color.FromArgb(24, 24, 24);
    public static readonly Color Text = Color.FromArgb(246, 246, 246);
    public static readonly Color Muted = Color.FromArgb(179, 179, 179);
    public static readonly Color Accent = Color.FromArgb(255, 78, 112);

    public static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }

    public static Color TrackColor(string value)
    {
        int hash = 17; foreach (char c in value ?? "") hash = unchecked(hash * 31 + c);
        Color[] colors = { Color.FromArgb(98, 72, 191), Color.FromArgb(33, 122, 113), Color.FromArgb(180, 64, 76), Color.FromArgb(39, 95, 145), Color.FromArgb(166, 96, 35) };
        return colors[(hash & Int32.MaxValue) % colors.Length];
    }
}

internal class MusicButton : Button
{
    bool hovering;
    public bool Emphasized { get; set; }
    public bool Selected { get; set; }
    public bool LeftAligned { get; set; }
    public int Radius { get; set; }
    public MusicButton() { Radius = 9; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent; ForeColor = MusicTheme.Text; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        Color fill = Emphasized ? (hovering ? Color.FromArgb(255, 101, 128) : MusicTheme.Accent) : (Selected ? MusicTheme.Raised : (hovering ? MusicTheme.Hover : BackColor));
        using (var path = MusicTheme.Rounded(bounds, Radius)) using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
        Rectangle textBounds = LeftAligned ? new Rectangle(31, 0, Width - 38, Height) : bounds;
        TextFormatFlags alignment = LeftAligned ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Emphasized ? Color.Black : (Selected ? MusicTheme.Text : ForeColor), alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -3, -3), ForeColor, fill);
    }
}

internal sealed class NavButton : MusicButton
{
    public NavButton() { Width = 194; Height = 42; Radius = 7; LeftAligned = true; Font = new Font("Segoe UI", 10, FontStyle.Bold); ForeColor = MusicTheme.Muted; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using (var brush = new SolidBrush(Selected ? MusicTheme.Accent : Color.Transparent)) e.Graphics.FillEllipse(brush, 13, Height / 2 - 3, 6, 6);
    }
}

internal sealed class ArtworkBox : Control
{
    public string KeyText { get; set; }
    public ArtworkBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using (var path = MusicTheme.Rounded(bounds, 6)) using (var brush = new LinearGradientBrush(bounds, MusicTheme.TrackColor(KeyText), Color.FromArgb(24, 24, 24), 45f)) e.Graphics.FillPath(brush, path);
        using (var pen = new Pen(Color.FromArgb(225, 255, 255, 255), Math.Max(2, Width / 28f)))
        {
            e.Graphics.DrawLine(pen, Width * .42f, Height * .30f, Width * .42f, Height * .68f); e.Graphics.DrawLine(pen, Width * .42f, Height * .30f, Width * .68f, Height * .24f);
            e.Graphics.FillEllipse(pen.Brush, Width * .27f, Height * .61f, Width * .18f, Height * .14f); e.Graphics.FillEllipse(pen.Brush, Width * .55f, Height * .54f, Width * .18f, Height * .14f);
        }
    }
}

internal sealed class MusicListView : ListView
{
    public MusicListView()
    {
        View = View.Details; FullRowSelect = true; MultiSelect = false; HideSelection = false; BorderStyle = BorderStyle.None; OwnerDraw = true; BackColor = MusicTheme.Canvas; ForeColor = MusicTheme.Text; HeaderStyle = ColumnHeaderStyle.Nonclickable;
        SmallImageList = new ImageList { ImageSize = new Size(1, 54), ColorDepth = ColorDepth.Depth32Bit }; SmallImageList.Images.Add(new Bitmap(1, 54)); SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var brush = new SolidBrush(MusicTheme.Canvas)) e.Graphics.FillRectangle(brush, e.Bounds); using (var pen = new Pen(Color.FromArgb(45, 255, 255, 255))) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        using (var font = new Font("Segoe UI", 8, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, e.Header.Text.ToUpperInvariant(), font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height), MusicTheme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
    protected override void OnDrawItem(DrawListViewItemEventArgs e) { }
    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        using (var brush = new SolidBrush(e.Item.Selected ? MusicTheme.Hover : MusicTheme.Canvas)) e.Graphics.FillRectangle(brush, e.Bounds); Rectangle text = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
        if (e.ColumnIndex == 1) { Rectangle art = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 7, 40, 40); using (var path = MusicTheme.Rounded(art, 5)) using (var brush = new SolidBrush(MusicTheme.TrackColor(e.SubItem.Text))) e.Graphics.FillPath(brush, path); using (var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255))) e.Graphics.FillEllipse(brush, art.X + 15, art.Y + 14, 10, 10); text.X += 48; text.Width -= 48; }
        Color color = e.ColumnIndex == 1 ? MusicTheme.Text : MusicTheme.Muted; using (var font = new Font("Segoe UI", 9, e.ColumnIndex == 1 ? FontStyle.Bold : FontStyle.Regular)) TextRenderer.DrawText(e.Graphics, e.SubItem.Text, font, text, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class QuickCard : Panel
{
    public QuickCard(string text, EventHandler click)
    {
        Width = 205; Height = 68; Margin = new Padding(0, 0, 12, 12); BackColor = MusicTheme.Raised; Cursor = Cursors.Hand;
        var art = new ArtworkBox { Dock = DockStyle.Left, Width = 68, KeyText = text }; var title = new Label { Dock = DockStyle.Fill, Text = text, ForeColor = MusicTheme.Text, Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(12, 0, 8, 0), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        Controls.Add(title); Controls.Add(art); foreach (Control c in Controls) c.Click += click; Click += click;
    }
}
