using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace YTMusicLite
{
    public static class UiPolish
    {
        public static void Attach(MainForm main)
        {
            if (main == null) return;
            SkinIconButtons(main);
            SkinVolumeGlyphs(main);
            SkinTray(main);

            MiniPlayerForm mini = GetPrivateField<MiniPlayerForm>(main, "miniPlayer");
            if (mini != null)
            {
                SkinIconButtons(mini);
                SkinVolumeGlyphs(mini);
            }
        }

        private static void SkinIconButtons(Control root)
        {
            List<LiteButton> buttons = new List<LiteButton>();
            CollectButtons(root, buttons);
            foreach (LiteButton original in buttons)
            {
                IconKind kind;
                if (!TryMapIcon(original.Text, out kind)) continue;

                Control parent = original.Parent;
                if (parent == null) continue;

                IconButton replacement = new IconButton();
                replacement.Icon = kind;
                replacement.Location = original.Location;
                replacement.Size = original.Size;
                replacement.Anchor = original.Anchor;
                replacement.BackColor = parent.BackColor;
                replacement.ForeColor = Color.FromArgb(218, 218, 218);
                replacement.ButtonStyle = PickStyle(original.Text, original);
                replacement.AccessibleName = string.IsNullOrEmpty(original.AccessibleName) ? original.Text : original.AccessibleName;
                replacement.TabStop = false;

                parent.Controls.Add(replacement);
                replacement.BringToFront();
                original.Visible = false;

                LiteButton capturedOriginal = original;
                IconButton capturedReplacement = replacement;
                replacement.Click += delegate { RaiseClick(capturedOriginal); };
                original.LocationChanged += delegate { capturedReplacement.Location = capturedOriginal.Location; };
                original.SizeChanged += delegate { capturedReplacement.Size = capturedOriginal.Size; };
                original.VisibleChanged += delegate
                {
                    if (capturedOriginal.Visible) capturedOriginal.Visible = false;
                };
                original.TextChanged += delegate
                {
                    IconKind changed;
                    if (TryMapIcon(capturedOriginal.Text, out changed)) capturedReplacement.Icon = changed;
                };
            }
        }

        private static void SkinVolumeGlyphs(Control root)
        {
            List<Label> labels = new List<Label>();
            CollectLabels(root, labels);
            foreach (Label label in labels)
            {
                if (!string.Equals(label.Text, "🔊", StringComparison.Ordinal)) continue;
                Control parent = label.Parent;
                if (parent == null) continue;

                IconGlyph glyph = new IconGlyph();
                glyph.Icon = IconKind.Volume;
                glyph.ForeColor = Color.FromArgb(160, 160, 160);
                glyph.Location = label.Location;
                glyph.Size = label.Size;
                parent.Controls.Add(glyph);
                glyph.BringToFront();
                label.Visible = false;

                Label captured = label;
                IconGlyph capturedGlyph = glyph;
                label.LocationChanged += delegate { capturedGlyph.Location = captured.Location; };
                label.SizeChanged += delegate { capturedGlyph.Size = captured.Size; };
            }
        }

        private static void SkinTray(MainForm main)
        {
            NotifyIcon tray = GetPrivateField<NotifyIcon>(main, "tray");
            if (tray == null || tray.ContextMenuStrip == null) return;

            ContextMenuStrip menu = tray.ContextMenuStrip;
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new LiteMenuRenderer();
            menu.BackColor = Color.FromArgb(24, 24, 24);
            menu.ForeColor = Color.FromArgb(235, 235, 235);
            menu.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            menu.Padding = new Padding(5, 5, 5, 5);
            menu.ShowImageMargin = true;
            menu.ImageScalingSize = new Size(16, 16);

            foreach (ToolStripItem item in menu.Items)
            {
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem == null) continue;
                menuItem.Padding = new Padding(4, 2, 7, 2);
                IconKind icon;
                if (TryMapMenuIcon(menuItem.Text, out icon))
                {
                    menuItem.Image = IconArt.CreateBitmap(icon, 16, Color.FromArgb(184, 184, 184));
                    menuItem.ImageScaling = ToolStripItemImageScaling.None;
                }
            }
        }

        private static IconButtonStyle PickStyle(string text, LiteButton original)
        {
            if (text == "▶" || text == "❚❚") return IconButtonStyle.Light;
            if (text == "×") return IconButtonStyle.Danger;
            if (original.Parent is MiniPlayerForm) return IconButtonStyle.Ghost;
            return IconButtonStyle.Ghost;
        }

        private static bool TryMapIcon(string text, out IconKind kind)
        {
            kind = IconKind.Play;
            if (text == "‹") { kind = IconKind.Back; return true; }
            if (text == "›") { kind = IconKind.Forward; return true; }
            if (text == "↻") { kind = IconKind.Reload; return true; }
            if (text == "⌂") { kind = IconKind.Home; return true; }
            if (text == "▱") { kind = IconKind.MiniPlayer; return true; }
            if (text == "⚙") { kind = IconKind.Settings; return true; }
            if (text == "⏮") { kind = IconKind.Previous; return true; }
            if (text == "▶") { kind = IconKind.Play; return true; }
            if (text == "❚❚") { kind = IconKind.Pause; return true; }
            if (text == "⏭") { kind = IconKind.Next; return true; }
            if (text == "▣") { kind = IconKind.Window; return true; }
            if (text == "×") { kind = IconKind.Close; return true; }
            return false;
        }

        private static bool TryMapMenuIcon(string text, out IconKind kind)
        {
            kind = IconKind.Window;
            if (text == "Show YT Music Lite") { kind = IconKind.Window; return true; }
            if (text == "Mini player") { kind = IconKind.MiniPlayer; return true; }
            if (text == "Play / Pause") { kind = IconKind.Play; return true; }
            if (text == "Sleep") { kind = IconKind.Sleep; return true; }
            if (text == "Settings") { kind = IconKind.Settings; return true; }
            if (text == "Exit") { kind = IconKind.Close; return true; }
            return false;
        }

        private static void RaiseClick(Control control)
        {
            try
            {
                MethodInfo method = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null) method.Invoke(control, new object[] { EventArgs.Empty });
            }
            catch
            {
            }
        }

        private static T GetPrivateField<T>(object instance, string name) where T : class
        {
            try
            {
                FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) return null;
                return field.GetValue(instance) as T;
            }
            catch
            {
                return null;
            }
        }

        private static void CollectButtons(Control root, List<LiteButton> result)
        {
            foreach (Control child in root.Controls)
            {
                LiteButton button = child as LiteButton;
                if (button != null) result.Add(button);
                CollectButtons(child, result);
            }
        }

        private static void CollectLabels(Control root, List<Label> result)
        {
            foreach (Control child in root.Controls)
            {
                Label label = child as Label;
                if (label != null) result.Add(label);
                CollectLabels(child, result);
            }
        }
    }
}
