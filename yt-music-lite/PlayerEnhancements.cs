using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace YTMusicLite
{
    public static class PlayerEnhancements
    {
        private static readonly List<PlayerEnhancementController> controllers = new List<PlayerEnhancementController>();

        public static void Attach(MainForm main)
        {
            if (main == null) return;
            Panel playerBar = GetPrivateField<Panel>(main, "playerBar");
            WebView2 web = GetPrivateField<WebView2>(main, "web");
            if (playerBar == null || web == null) return;

            PlayerEnhancementController controller = new PlayerEnhancementController(main, playerBar, web);
            controllers.Add(controller);
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

        private sealed class PlayerEnhancementController
        {
            private readonly MainForm main;
            private readonly Panel bar;
            private readonly WebView2 web;
            private readonly ToolTip tips;
            private readonly SeekBar volume;
            private readonly PictureBox artwork;
            private readonly Label title;
            private readonly Label artist;

            private readonly PlayerFeatureButton like;
            private readonly PlayerFeatureButton dislike;
            private readonly PlayerFeatureButton more;
            private readonly PlayerFeatureButton shuffle;
            private readonly PlayerFeatureButton repeat;
            private readonly PlayerFeatureButton lyrics;
            private readonly PlayerFeatureButton queue;
            private readonly PlayerFeatureButton mute;

            public PlayerEnhancementController(MainForm owner, Panel playerBar, WebView2 webView)
            {
                main = owner;
                bar = playerBar;
                web = webView;
                tips = new ToolTip();
                tips.ShowAlways = true;

                volume = GetPrivateField<SeekBar>(main, "volume");
                artwork = GetPrivateField<PictureBox>(main, "artwork");
                title = GetPrivateField<Label>(main, "nowTitle");
                artist = GetPrivateField<Label>(main, "nowArtist");

                like = AddButton(PlayerFeatureIcon.Like, "Like");
                dislike = AddButton(PlayerFeatureIcon.Dislike, "Dislike");
                more = AddButton(PlayerFeatureIcon.More, "More actions");
                shuffle = AddButton(PlayerFeatureIcon.Shuffle, "Shuffle");
                repeat = AddButton(PlayerFeatureIcon.Repeat, "Repeat");
                lyrics = AddButton(PlayerFeatureIcon.Lyrics, "Lyrics");
                queue = AddButton(PlayerFeatureIcon.Queue, "Queue / Up next");
                mute = AddButton(PlayerFeatureIcon.Mute, "Mute / Unmute");

                like.Click += async delegate { if (await ClickLikeAsync(false)) { like.Active = !like.Active; if (like.Active) dislike.Active = false; } };
                dislike.Click += async delegate { if (await ClickLikeAsync(true)) { dislike.Active = !dislike.Active; if (dislike.Active) like.Active = false; } };
                more.Click += async delegate { await ClickMoreAsync(); };
                shuffle.Click += async delegate { if (await ClickIntentAsync("shuffle")) shuffle.Active = !shuffle.Active; };
                repeat.Click += async delegate { if (await ClickIntentAsync("repeat")) repeat.Active = !repeat.Active; };
                lyrics.Click += async delegate { await OpenLyricsAsync(); };
                queue.Click += async delegate { await ClickIntentAsync("queue"); };
                mute.Click += async delegate { mute.Active = await ToggleMuteAsync(); };

                MakeCurrentTrackClickable();
                HideLegacyVolumeGlyph();
                bar.Resize += delegate { Layout(); };
                Layout();
            }

            private PlayerFeatureButton AddButton(PlayerFeatureIcon icon, string tip)
            {
                PlayerFeatureButton button = new PlayerFeatureButton();
                button.Icon = icon;
                button.Size = new Size(34, 34);
                button.BackColor = bar.BackColor;
                button.ForeColor = Color.FromArgb(188, 188, 188);
                bar.Controls.Add(button);
                button.BringToFront();
                tips.SetToolTip(button, tip);
                return button;
            }

            private void MakeCurrentTrackClickable()
            {
                EventHandler open = delegate { OpenCurrentTrack(); };
                if (artwork != null)
                {
                    artwork.Cursor = Cursors.Hand;
                    artwork.Click += open;
                    tips.SetToolTip(artwork, "Open current track");
                }
                if (title != null)
                {
                    title.Cursor = Cursors.Hand;
                    title.Click += open;
                    tips.SetToolTip(title, "Open current track");
                }
                if (artist != null)
                {
                    artist.Cursor = Cursors.Hand;
                    artist.Click += open;
                    tips.SetToolTip(artist, "Open current track");
                }
            }

            private void HideLegacyVolumeGlyph()
            {
                foreach (Control control in bar.Controls)
                {
                    Label label = control as Label;
                    if (label != null && string.Equals(label.Text, "🔊", StringComparison.Ordinal))
                    {
                        label.Visible = false;
                    }

                    IconGlyph glyph = control as IconGlyph;
                    if (glyph != null && glyph.Icon == IconKind.Volume)
                    {
                        glyph.Visible = false;
                    }
                }
            }

            private void Layout()
            {
                int width = bar.ClientSize.Width;
                bool compact = width < 1080;

                like.Visible = !compact;
                dislike.Visible = !compact;
                shuffle.Visible = !compact;
                repeat.Visible = !compact;
                lyrics.Visible = !compact;

                like.Location = new Point(342, 14);
                dislike.Location = new Point(378, 14);
                more.Location = new Point(compact ? 340 : 414, 14);

                int volumeWidth = compact ? 92 : 112;
                int volumeX = Math.Max(720, width - volumeWidth - 24);
                if (volume != null)
                {
                    volume.Location = new Point(volumeX, 34);
                    volume.Size = new Size(volumeWidth, 18);
                    volume.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    volume.BringToFront();
                }

                mute.Location = new Point(volumeX - 38, 26);
                queue.Location = new Point(volumeX - 76, 26);
                lyrics.Location = new Point(volumeX - 114, 26);
                repeat.Location = new Point(volumeX - 152, 26);
                shuffle.Location = new Point(volumeX - 190, 26);

                more.BringToFront();
                like.BringToFront();
                dislike.BringToFront();
                shuffle.BringToFront();
                repeat.BringToFront();
                lyrics.BringToFront();
                queue.BringToFront();
                mute.BringToFront();
            }

            private async Task<bool> ClickLikeAsync(bool negative)
            {
                string index = negative ? "1" : "0";
                string labels = negative
                    ? "['dislike','không thích','not like']"
                    : "['like','thích']";

                string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    if (!bar) return false;
                    const renderer = bar.querySelector('ytmusic-like-button-renderer');
                    if (!renderer) return false;
                    const candidates = Array.from(renderer.querySelectorAll('button, tp-yt-paper-icon-button, yt-icon-button'));
                    const words = " + labels + @";
                    let button = candidates.find(b => {
                        const label = ((b.getAttribute('aria-label') || b.title || '') + ' ' + (b.textContent || '')).toLowerCase();
                        return words.some(w => label.indexOf(w) >= 0);
                    });
                    if (!button && candidates.length > " + index + @") button = candidates[" + index + @"];
                    if (!button) return false;
                    button.click();
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task<bool> ClickMoreAsync()
            {
                const string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    if (!bar) return false;
                    const menu = bar.querySelector('ytmusic-menu-renderer');
                    if (!menu) return false;
                    const button = menu.querySelector('button, tp-yt-paper-icon-button, yt-icon-button');
                    if (!button) return false;
                    button.click();
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task<bool> ClickIntentAsync(string intent)
            {
                string selectors;
                string tokens;
                if (intent == "shuffle")
                {
                    selectors = "'[class*=shuffle], #shuffle, [aria-label*=shuffle i], [aria-label*=\\"ngẫu nhiên\\" i], [aria-label*=\\"trộn\\" i]'";
                    tokens = "['shuffle','ngẫu nhiên','trộn']";
                }
                else if (intent == "repeat")
                {
                    selectors = "'[class*=repeat], #repeat, [aria-label*=repeat i], [aria-label*=\\"lặp\\" i]'";
                    tokens = "['repeat','lặp']";
                }
                else
                {
                    selectors = "'[class*=queue], #queue, [aria-label*=queue i], [aria-label*=\\"hàng đợi\\" i], [aria-label*=\\"up next\\" i]'";
                    tokens = "['queue','hàng đợi','up next']";
                }

                string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    const scope = bar || document;
                    let button = null;
                    try { button = scope.querySelector(" + selectors + @"); } catch (_) {}
                    if (!button) {
                        const words = " + tokens + @";
                        const all = Array.from(scope.querySelectorAll('button, tp-yt-paper-icon-button, yt-icon-button'));
                        button = all.find(b => {
                            const label = ((b.getAttribute('aria-label') || b.title || '') + ' ' + (b.textContent || '')).toLowerCase();
                            return words.some(w => label.indexOf(w) >= 0);
                        });
                    }
                    if (!button) return false;
                    button.click();
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task OpenLyricsAsync()
            {
                OpenCurrentTrack();
                await Task.Delay(300);
                const string script = @"(() => {
                    const tabs = Array.from(document.querySelectorAll('tp-yt-paper-tab, ytmusic-player-page tp-yt-paper-tab, [role=tab]'));
                    const target = tabs.find(t => {
                        const text = (t.textContent || '').trim().toLowerCase();
                        const label = (t.getAttribute('aria-label') || '').toLowerCase();
                        return text.indexOf('lyrics') >= 0 || text.indexOf('lời bài hát') >= 0 || label.indexOf('lyrics') >= 0 || label.indexOf('lời bài hát') >= 0;
                    });
                    if (!target) return false;
                    target.click();
                    return true;
                })();";
                await ExecuteBooleanAsync(script);
            }

            private async Task<bool> ToggleMuteAsync()
            {
                const string script = @"(() => {
                    const media = document.querySelector('video, audio');
                    if (!media) return false;
                    media.muted = !media.muted;
                    return media.muted;
                })();";
                try
                {
                    if (web.CoreWebView2 == null) return false;
                    string raw = await web.CoreWebView2.ExecuteScriptAsync(script);
                    return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            private async Task<bool> ExecuteBooleanAsync(string script)
            {
                try
                {
                    if (web.CoreWebView2 == null) return false;
                    string raw = await web.CoreWebView2.ExecuteScriptAsync(script);
                    return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            private void OpenCurrentTrack()
            {
                try
                {
                    MethodInfo method = main.GetType().GetMethod("OpenCurrentTrack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        method.Invoke(main, null);
                        return;
                    }
                }
                catch
                {
                }

                try
                {
                    main.RestoreMainWindow();
                    if (web.CoreWebView2 == null) return;
                    const string script = @"(() => {
                        const bar = document.querySelector('ytmusic-player-bar');
                        if (!bar) return false;
                        const link = bar.querySelector('a[href*=\\"watch\\"], .title a, a.image-link');
                        if (link) { link.click(); return true; }
                        const target = bar.querySelector('.title, yt-formatted-string.title, img');
                        if (target) { target.click(); return true; }
                        return false;
                    })();";
                    web.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch
                {
                }
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
    }

    internal enum PlayerFeatureIcon
    {
        Like,
        Dislike,
        More,
        Shuffle,
        Repeat,
        Lyrics,
        Queue,
        Mute
    }

    internal sealed class PlayerFeatureButton : Control
    {
        private bool hovered;
        private bool pressed;
        private bool active;
        private PlayerFeatureIcon icon;

        public PlayerFeatureButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(34, 34);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public PlayerFeatureIcon Icon
        {
            get { return icon; }
            set { icon = value; Invalidate(); }
        }

        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color iconColor = active ? Color.FromArgb(255, 58, 78) : ForeColor;

            if (hovered || pressed)
            {
                Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);
                using (GraphicsPath path = RoundedRect(rect, 8))
                using (SolidBrush brush = new SolidBrush(pressed ? Color.FromArgb(48, 48, 48) : Color.FromArgb(34, 34, 34)))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            Rectangle bounds = new Rectangle((Width - 18) / 2, (Height - 18) / 2, 18, 18);
            DrawIcon(e.Graphics, icon, bounds, iconColor);
        }

        private static void DrawIcon(Graphics g, PlayerFeatureIcon kind, Rectangle b, Color color)
        {
            float cx = b.Left + b.Width / 2f;
            float cy = b.Top + b.Height / 2f;
            using (Pen pen = new Pen(color, 1.7f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (kind == PlayerFeatureIcon.More)
                {
                    g.FillEllipse(brush, cx - 7f, cy - 1.5f, 3f, 3f);
                    g.FillEllipse(brush, cx - 1.5f, cy - 1.5f, 3f, 3f);
                    g.FillEllipse(brush, cx + 4f, cy - 1.5f, 3f, 3f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Like || kind == PlayerFeatureIcon.Dislike)
                {
                    GraphicsState state = g.Save();
                    if (kind == PlayerFeatureIcon.Dislike)
                    {
                        g.TranslateTransform(cx, cy);
                        g.RotateTransform(180f);
                        g.TranslateTransform(-cx, -cy);
                    }
                    PointF[] thumb = new PointF[] {
                        new PointF(cx - 7f, cy + 1f), new PointF(cx - 3f, cy + 1f),
                        new PointF(cx, cy - 6f), new PointF(cx + 3f, cy - 6f),
                        new PointF(cx + 2f, cy - 1f), new PointF(cx + 7f, cy - 1f),
                        new PointF(cx + 6f, cy + 6f), new PointF(cx - 3f, cy + 6f),
                        new PointF(cx - 3f, cy + 1f)
                    };
                    g.DrawLines(pen, thumb);
                    g.DrawLine(pen, cx - 7f, cy + 1f, cx - 7f, cy + 6f);
                    g.DrawLine(pen, cx - 7f, cy + 6f, cx - 3f, cy + 6f);
                    g.Restore(state);
                    return;
                }

                if (kind == PlayerFeatureIcon.Shuffle)
                {
                    g.DrawLine(pen, cx - 7f, cy - 5f, cx - 4f, cy - 5f);
                    g.DrawBezier(pen, cx - 4f, cy - 5f, cx, cy - 5f, cx + 1f, cy + 5f, cx + 6f, cy + 5f);
                    g.DrawLine(pen, cx - 7f, cy + 5f, cx - 4f, cy + 5f);
                    g.DrawBezier(pen, cx - 4f, cy + 5f, cx, cy + 5f, cx + 1f, cy - 5f, cx + 6f, cy - 5f);
                    g.DrawLine(pen, cx + 3f, cy - 8f, cx + 7f, cy - 5f);
                    g.DrawLine(pen, cx + 3f, cy - 2f, cx + 7f, cy - 5f);
                    g.DrawLine(pen, cx + 3f, cy + 2f, cx + 7f, cy + 5f);
                    g.DrawLine(pen, cx + 3f, cy + 8f, cx + 7f, cy + 5f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Repeat)
                {
                    g.DrawArc(pen, cx - 7f, cy - 6f, 14f, 9f, 190f, 165f);
                    g.DrawArc(pen, cx - 7f, cy - 3f, 14f, 9f, 10f, 165f);
                    g.DrawLine(pen, cx + 4f, cy - 7f, cx + 7f, cy - 4f);
                    g.DrawLine(pen, cx + 7f, cy - 4f, cx + 3f, cy - 3f);
                    g.DrawLine(pen, cx - 4f, cy + 7f, cx - 7f, cy + 4f);
                    g.DrawLine(pen, cx - 7f, cy + 4f, cx - 3f, cy + 3f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Lyrics)
                {
                    g.DrawLine(pen, cx - 6f, cy - 7f, cx + 6f, cy - 7f);
                    g.DrawLine(pen, cx - 6f, cy - 2f, cx + 4f, cy - 2f);
                    g.DrawLine(pen, cx - 6f, cy + 3f, cx + 6f, cy + 3f);
                    g.DrawLine(pen, cx - 6f, cy + 8f, cx + 1f, cy + 8f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Queue)
                {
                    g.DrawLine(pen, cx - 7f, cy - 5f, cx + 2f, cy - 5f);
                    g.DrawLine(pen, cx - 7f, cy, cx + 2f, cy);
                    g.DrawLine(pen, cx - 7f, cy + 5f, cx + 2f, cy + 5f);
                    g.FillPolygon(brush, new PointF[] {
                        new PointF(cx + 5f, cy + 1f), new PointF(cx + 5f, cy + 8f), new PointF(cx + 10f, cy + 4.5f)
                    });
                    return;
                }

                if (kind == PlayerFeatureIcon.Mute)
                {
                    PointF[] speaker = new PointF[] {
                        new PointF(cx - 8f, cy - 3f), new PointF(cx - 4f, cy - 3f),
                        new PointF(cx + 1f, cy - 7f), new PointF(cx + 1f, cy + 7f),
                        new PointF(cx - 4f, cy + 3f), new PointF(cx - 8f, cy + 3f)
                    };
                    g.FillPolygon(brush, speaker);
                    g.DrawLine(pen, cx + 4f, cy - 5f, cx + 10f, cy + 5f);
                    g.DrawLine(pen, cx + 10f, cy - 5f, cx + 4f, cy + 5f);
                }
            }
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
}
