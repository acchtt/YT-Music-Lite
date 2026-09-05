using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace YTMusicLite
{
    public static class UiPolish
    {
        public static void Attach(MainForm main)
        {
            if (main == null) return;
            ApplyBranding(main);
            MakeCurrentTrackClickable(main);
            SkinIconButtons(main);
            SkinVolumeGlyphs(main);
            SkinTray(main);

            MiniPlayerForm mini = GetPrivateField<MiniPlayerForm>(main, "miniPlayer");
            if (mini != null)
            {
                MakeMiniTrackClickable(main, mini);
                SkinIconButtons(mini);
                SkinVolumeGlyphs(mini);
            }
        }

        private static void ApplyBranding(MainForm main)
        {
            Panel topBar = GetPrivateField<Panel>(main, "topBar");
            if (topBar == null) return;

            List<Label> labels = new List<Label>();
            CollectLabels(topBar, labels);
            foreach (Label label in labels)
            {
                if (label.Text == "●" || label.Text == "YT Music Lite") label.Visible = false;
            }

            BrandLogoControl logo = new BrandLogoControl();
            logo.Location = new Point(8, 8);
            logo.Size = new Size(150, 38);
            logo.Click += delegate
            {
                WebView2 web = GetPrivateField<WebView2>(main, "web");
                if (web != null && web.CoreWebView2 != null) web.CoreWebView2.Navigate("https://music.youtube.com/");
            };
            topBar.Controls.Add(logo);
            logo.BringToFront();
        }

        private static void MakeCurrentTrackClickable(MainForm main)
        {
            PictureBox artwork = GetPrivateField<PictureBox>(main, "artwork");
            Label title = GetPrivateField<Label>(main, "nowTitle");
            Label artist = GetPrivateField<Label>(main, "nowArtist");
            ToolTip tips = GetPrivateField<ToolTip>(main, "tips");

            WireTrackControl(main, artwork, tips);
            WireTrackControl(main, title, tips);
            WireTrackControl(main, artist, tips);
        }

        private static void MakeMiniTrackClickable(MainForm main, MiniPlayerForm mini)
        {
            PictureBox artwork = GetPrivateField<PictureBox>(mini, "artwork");
            Label title = GetPrivateField<Label>(mini, "title");
            Label artist = GetPrivateField<Label>(mini, "artist");
            ToolTip tips = GetPrivateField<ToolTip>(mini, "tips");

            WireTrackControl(main, artwork, tips);
            WireTrackControl(main, title, tips);
            WireTrackControl(main, artist, tips);
        }

        private static void WireTrackControl(MainForm main, Control control, ToolTip tips)
        {
            if (control == null) return;
            control.Cursor = Cursors.Hand;
            control.Click += delegate { OpenCurrentTrack(main); };
            if (tips != null) tips.SetToolTip(control, "Open current track");
        }

        private static async void OpenCurrentTrack(MainForm main)
        {
            if (main == null) return;
            main.RestoreMainWindow();

            WebView2 web = GetPrivateField<WebView2>(main, "web");
            if (web == null || web.CoreWebView2 == null) return;

            string script = @"(() => {
                const bar = document.querySelector('ytmusic-player-bar');
                if (!bar) return 'no-player';
                const link =
                    bar.querySelector('a[href*=""watch""]') ||
                    bar.querySelector('yt-formatted-string.title a') ||
                    (bar.querySelector('.title') && bar.querySelector('.title').closest('a')) ||
                    (bar.querySelector('img') && bar.querySelector('img').closest('a'));
                if (link) {
                    link.click();
                    return 'opened';
                }
                const target = bar.querySelector('.title, yt-formatted-string.title, .thumbnail-image-wrapper, img');
                if (target && typeof target.click === 'function') {
                    target.click();
                    return 'clicked';
                }
                return 'current';
            })();";

            try { await web.CoreWebView2.ExecuteScriptAsync(script); }
            catch { }
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
            bool hasOpenTrack = false;
            int insertAt = Math.Min(2, menu.Items.Count);
            for (int i = 0; i < menu.Items.Count; i++)
            {
                ToolStripMenuItem existing = menu.Items[i] as ToolStripMenuItem;
                if (existing != null && existing.Text == "Open current track") hasOpenTrack = true;
                if (existing != null && existing.Text == "Mini player") insertAt = i + 1;
            }
            if (!hasOpenTrack)
            {
                ToolStripMenuItem openTrack = new ToolStripMenuItem("Open current track");
                openTrack.Click += delegate { OpenCurrentTrack(main); };
                menu.Items.Insert(insertAt, openTrack);
            }

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
            if (text == "Open current track") { kind = IconKind.Play; return true; }
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
