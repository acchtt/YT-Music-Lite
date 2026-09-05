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
            catch { return null; }
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
            private readonly Timer stateSync;

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
                tips.AutoPopDelay = 4500;
                tips.InitialDelay = 420;
                tips.ReshowDelay = 80;

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
                mute = AddButton(PlayerFeatureIcon.Mute, "Mute");

                like.Click += async delegate { if (await ClickLikeAsync(false)) await DelayedSyncAsync(); };
                dislike.Click += async delegate { if (await ClickLikeAsync(true)) await DelayedSyncAsync(); };
                more.Click += async delegate { await ClickMoreAsync(); };
                shuffle.Click += async delegate { if (await ClickIntentAsync("shuffle")) await DelayedSyncAsync(); };
                repeat.Click += async delegate { if (await ClickIntentAsync("repeat")) await DelayedSyncAsync(); };
                lyrics.Click += async delegate { await OpenLyricsAsync(); };
                queue.Click += async delegate { await ClickIntentAsync("queue"); };
                mute.Click += async delegate { await ToggleMuteAsync(); await DelayedSyncAsync(); };

                MakeCurrentTrackClickable();
                HideLegacyVolumeGlyph();

                stateSync = new Timer();
                stateSync.Interval = 1200;
                stateSync.Tick += async delegate { await SyncStateAsync(); };
                stateSync.Start();

                bar.Resize += delegate { Layout(); };
                Layout();
            }

            private PlayerFeatureButton AddButton(PlayerFeatureIcon icon, string tip)
            {
                PlayerFeatureButton button = new PlayerFeatureButton();
                button.Icon = icon;
                button.Size = new Size(36, 36);
                button.BackColor = bar.BackColor;
                button.ForeColor = Color.FromArgb(174, 174, 174);
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
                    if (label != null && string.Equals(label.Text, "🔊", StringComparison.Ordinal)) label.Visible = false;
                    IconGlyph glyph = control as IconGlyph;
                    if (glyph != null && glyph.Icon == IconKind.Volume) glyph.Visible = false;
                }
            }

            private void Layout()
            {
                int width = bar.ClientSize.Width;
                int center = width / 2;
                bool roomy = width >= 1180;
                bool medium = width >= 1020;
                bool compact = width < 920;

                like.Visible = roomy;
                dislike.Visible = roomy;
                more.Visible = medium;
                lyrics.Visible = roomy;
                queue.Visible = medium;
                mute.Visible = true;
                shuffle.Visible = !compact;
                repeat.Visible = !compact;

                if (roomy)
                {
                    like.Location = new Point(340, 17);
                    dislike.Location = new Point(378, 17);
                    more.Location = new Point(416, 17);
                }
                else if (medium)
                {
                    more.Location = new Point(342, 17);
                }

                shuffle.Location = new Point(center - 128, 19);
                repeat.Location = new Point(center + 92, 19);

                int volumeWidth = width >= 1100 ? 116 : 92;
                int volumeX = Math.Max(center + 180, width - volumeWidth - 22);
                if (volume != null)
                {
                    volume.Location = new Point(volumeX, 36);
                    volume.Size = new Size(volumeWidth, 18);
                    volume.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    volume.BringToFront();
                }

                mute.Location = new Point(volumeX - 40, 27);
                queue.Location = new Point(volumeX - 80, 27);
                lyrics.Location = new Point(volumeX - 120, 27);

                like.BringToFront();
                dislike.BringToFront();
                more.BringToFront();
                shuffle.BringToFront();
                repeat.BringToFront();
                lyrics.BringToFront();
                queue.BringToFront();
                mute.BringToFront();
            }

            private async Task DelayedSyncAsync()
            {
                await Task.Delay(180);
                await SyncStateAsync();
            }

            private async Task SyncStateAsync()
            {
                if (web.CoreWebView2 == null) return;
                const string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    const media = document.querySelector('video, audio');
                    const stateFor = (words, scope) => {
                        if (!scope) return false;
                        const nodes = Array.from(scope.querySelectorAll('button, tp-yt-paper-icon-button, yt-icon-button'));
                        const target = nodes.find(b => {
                            const cls = typeof b.className === 'string' ? b.className : '';
                            const s = ((b.getAttribute('aria-label') || '') + ' ' + (b.title || '') + ' ' + cls).toLowerCase();
                            return words.some(w => s.indexOf(w) >= 0);
                        });
                        if (!target) return false;
                        const pressed = (target.getAttribute('aria-pressed') || '').toLowerCase();
                        const checked = (target.getAttribute('aria-checked') || '').toLowerCase();
                        const label = ((target.getAttribute('aria-label') || '') + ' ' + (target.title || '')).toLowerCase();
                        return pressed === 'true' || checked === 'true' || /(^|\s)(active|selected)(\s|$)/i.test(target.className || '') || label.indexOf('turn off') >= 0 || label.indexOf('disable') >= 0;
                    };
                    const likeRenderer = bar ? bar.querySelector('ytmusic-like-button-renderer') : null;
                    const like = stateFor(['like','thích'], likeRenderer);
                    const dislike = stateFor(['dislike','không thích'], likeRenderer);
                    const shuffle = stateFor(['shuffle','ngẫu nhiên','trộn'], bar || document);
                    const repeat = stateFor(['repeat','lặp'], bar || document);
                    const muted = !!(media && media.muted);
                    return [like, dislike, shuffle, repeat, muted].map(v => v ? '1' : '0').join('|');
                })();";

                try
                {
                    string raw = await web.CoreWebView2.ExecuteScriptAsync(script);
                    if (string.IsNullOrEmpty(raw)) return;
                    string value = raw.Trim();
                    if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    {
                        value = value.Substring(1, value.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
                    }
                    string[] parts = value.Split('|');
                    if (parts.Length < 5) return;
                    like.Active = parts[0] == "1";
                    dislike.Active = parts[1] == "1";
                    shuffle.Active = parts[2] == "1";
                    repeat.Active = parts[3] == "1";
                    mute.Active = parts[4] == "1";
                    tips.SetToolTip(mute, mute.Active ? "Unmute" : "Mute");
                }
                catch { }
            }

            private async Task<bool> ClickLikeAsync(bool negative)
            {
                string words = negative ? "['dislike','không thích','not like']" : "['like','thích']";
                string fallbackIndex = negative ? "1" : "0";
                string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    if (!bar) return false;
                    const renderer = bar.querySelector('ytmusic-like-button-renderer');
                    if (!renderer) return false;
                    const buttons = Array.from(renderer.querySelectorAll('button, tp-yt-paper-icon-button, yt-icon-button'));
                    const words = " + words + @";
                    let target = buttons.find(b => {
                        const s = ((b.getAttribute('aria-label') || '') + ' ' + (b.title || '') + ' ' + (b.textContent || '')).toLowerCase();
                        return words.some(w => s.indexOf(w) >= 0);
                    });
                    if (!target && buttons.length > " + fallbackIndex + @") target = buttons[" + fallbackIndex + @"];
                    if (!target) return false;
                    target.click();
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task<bool> ClickMoreAsync()
            {
                const string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    if (!bar) return false;
                    const renderer = bar.querySelector('ytmusic-menu-renderer');
                    if (!renderer) return false;
                    const target = renderer.querySelector('button, tp-yt-paper-icon-button, yt-icon-button');
                    if (!target) return false;
                    target.click();
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task<bool> ClickIntentAsync(string intent)
            {
                string words;
                if (intent == "shuffle") words = "['shuffle','ngẫu nhiên','trộn']";
                else if (intent == "repeat") words = "['repeat','lặp']";
                else words = "['queue','hàng đợi','up next']";
                string script = @"(() => {
                    const bar = document.querySelector('ytmusic-player-bar');
                    const scope = bar || document;
                    const words = " + words + @";
                    const nodes = Array.from(scope.querySelectorAll('button, tp-yt-paper-icon-button, yt-icon-button'));
                    const target = nodes.find(b => {
                        const cls = typeof b.className === 'string' ? b.className : '';
                        const id = b.id || '';
                        const s = ((b.getAttribute('aria-label') || '') + ' ' + (b.title || '') + ' ' + (b.textContent || '') + ' ' + cls + ' ' + id).toLowerCase();
                        return words.some(w => s.indexOf(w) >= 0);
                    });
                    if (!target) return false;
                    target.click();
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task OpenLyricsAsync()
            {
                OpenCurrentTrack();
                await Task.Delay(350);
                const string script = @"(() => {
                    const tabs = Array.from(document.querySelectorAll('tp-yt-paper-tab, ytmusic-player-page tp-yt-paper-tab, [role=tab]'));
                    const target = tabs.find(t => {
                        const s = ((t.textContent || '') + ' ' + (t.getAttribute('aria-label') || '')).trim().toLowerCase();
                        return s.indexOf('lyrics') >= 0 || s.indexOf('lời bài hát') >= 0;
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
                    return true;
                })();";
                return await ExecuteBooleanAsync(script);
            }

            private async Task<bool> ExecuteBooleanAsync(string script)
            {
                try
                {
                    if (web.CoreWebView2 == null) return false;
                    string raw = await web.CoreWebView2.ExecuteScriptAsync(script);
                    return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }

            private void OpenCurrentTrack()
            {
                try
                {
                    MethodInfo method = main.GetType().GetMethod("OpenCurrentTrack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null) { method.Invoke(main, null); return; }
                }
                catch { }

                try
                {
                    main.RestoreMainWindow();
                    if (web.CoreWebView2 == null) return;
                    const string script = @"(() => {
                        const bar = document.querySelector('ytmusic-player-bar');
                        if (!bar) return false;
                        const links = Array.from(bar.querySelectorAll('a'));
                        let target = links.find(a => (a.getAttribute('href') || '').indexOf('watch') >= 0);
                        if (!target) target = bar.querySelector('.title, yt-formatted-string.title, img');
                        if (!target) return false;
                        target.click();
                        return true;
                    })();";
                    web.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch { }
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
            Size = new Size(36, 36);
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
            set { if (active == value) return; active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color iconColor = active ? Color.FromArgb(238, 238, 238) : ForeColor;
            Color hoverFill = pressed ? Color.FromArgb(45, 45, 45) : Color.FromArgb(31, 31, 31);

            if (hovered || pressed || active)
            {
                Rectangle rect = new Rectangle(2, 2, Width - 4, Height - 4);
                using (SolidBrush brush = new SolidBrush(active && !hovered && !pressed ? Color.FromArgb(25, 25, 25) : hoverFill))
                {
                    e.Graphics.FillEllipse(brush, rect);
                }
            }

            Rectangle bounds = new Rectangle((Width - 17) / 2, (Height - 17) / 2 - 1, 17, 17);
            DrawIcon(e.Graphics, icon, bounds, iconColor, active);

            if (active)
            {
                using (SolidBrush accent = new SolidBrush(Color.FromArgb(255, 58, 78)))
                {
                    e.Graphics.FillEllipse(accent, Width / 2f - 2f, Height - 5f, 4f, 4f);
                }
            }
        }

        private static void DrawIcon(Graphics g, PlayerFeatureIcon kind, Rectangle b, Color color, bool active)
        {
            float cx = b.Left + b.Width / 2f;
            float cy = b.Top + b.Height / 2f;
            using (Pen pen = new Pen(color, 1.55f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (kind == PlayerFeatureIcon.More)
                {
                    g.FillEllipse(brush, cx - 6.5f, cy - 1.4f, 2.8f, 2.8f);
                    g.FillEllipse(brush, cx - 1.4f, cy - 1.4f, 2.8f, 2.8f);
                    g.FillEllipse(brush, cx + 3.7f, cy - 1.4f, 2.8f, 2.8f);
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
                        new PointF(cx - 6.5f, cy + 1f), new PointF(cx - 2.5f, cy + 1f),
                        new PointF(cx, cy - 5.8f), new PointF(cx + 2.8f, cy - 5.8f),
                        new PointF(cx + 2f, cy - 1f), new PointF(cx + 6.5f, cy - 1f),
                        new PointF(cx + 5.5f, cy + 5.5f), new PointF(cx - 2.5f, cy + 5.5f)
                    };
                    g.DrawLines(pen, thumb);
                    g.DrawLine(pen, cx - 6.5f, cy + 1f, cx - 6.5f, cy + 5.5f);
                    g.DrawLine(pen, cx - 6.5f, cy + 5.5f, cx - 2.5f, cy + 5.5f);
                    g.Restore(state);
                    return;
                }

                if (kind == PlayerFeatureIcon.Shuffle)
                {
                    g.DrawLine(pen, cx - 6.5f, cy - 4.5f, cx - 3.8f, cy - 4.5f);
                    g.DrawBezier(pen, cx - 3.8f, cy - 4.5f, cx, cy - 4.5f, cx + 1f, cy + 4.5f, cx + 5.5f, cy + 4.5f);
                    g.DrawLine(pen, cx - 6.5f, cy + 4.5f, cx - 3.8f, cy + 4.5f);
                    g.DrawBezier(pen, cx - 3.8f, cy + 4.5f, cx, cy + 4.5f, cx + 1f, cy - 4.5f, cx + 5.5f, cy - 4.5f);
                    g.DrawLine(pen, cx + 3f, cy - 7f, cx + 6.5f, cy - 4.5f);
                    g.DrawLine(pen, cx + 3f, cy - 2f, cx + 6.5f, cy - 4.5f);
                    g.DrawLine(pen, cx + 3f, cy + 2f, cx + 6.5f, cy + 4.5f);
                    g.DrawLine(pen, cx + 3f, cy + 7f, cx + 6.5f, cy + 4.5f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Repeat)
                {
                    g.DrawArc(pen, cx - 6.5f, cy - 5.5f, 13f, 8f, 190f, 165f);
                    g.DrawArc(pen, cx - 6.5f, cy - 2.5f, 13f, 8f, 10f, 165f);
                    g.DrawLine(pen, cx + 3.5f, cy - 6.5f, cx + 6.5f, cy - 4f);
                    g.DrawLine(pen, cx + 6.5f, cy - 4f, cx + 2.8f, cy - 3f);
                    g.DrawLine(pen, cx - 3.5f, cy + 6.5f, cx - 6.5f, cy + 4f);
                    g.DrawLine(pen, cx - 6.5f, cy + 4f, cx - 2.8f, cy + 3f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Lyrics)
                {
                    g.DrawLine(pen, cx - 5.5f, cy - 6f, cx + 5.5f, cy - 6f);
                    g.DrawLine(pen, cx - 5.5f, cy - 2f, cx + 3.5f, cy - 2f);
                    g.DrawLine(pen, cx - 5.5f, cy + 2f, cx + 5.5f, cy + 2f);
                    g.DrawLine(pen, cx - 5.5f, cy + 6f, cx + 1f, cy + 6f);
                    return;
                }

                if (kind == PlayerFeatureIcon.Queue)
                {
                    g.DrawLine(pen, cx - 6f, cy - 4.5f, cx + 1f, cy - 4.5f);
                    g.DrawLine(pen, cx - 6f, cy, cx + 1f, cy);
                    g.DrawLine(pen, cx - 6f, cy + 4.5f, cx + 1f, cy + 4.5f);
                    g.FillPolygon(brush, new PointF[] { new PointF(cx + 4f, cy + 1f), new PointF(cx + 4f, cy + 6.5f), new PointF(cx + 8f, cy + 3.75f) });
                    return;
                }

                if (kind == PlayerFeatureIcon.Mute)
                {
                    PointF[] speaker = new PointF[] {
                        new PointF(cx - 7f, cy - 2.5f), new PointF(cx - 3.5f, cy - 2.5f),
                        new PointF(cx + .5f, cy - 6f), new PointF(cx + .5f, cy + 6f),
                        new PointF(cx - 3.5f, cy + 2.5f), new PointF(cx - 7f, cy + 2.5f)
                    };
                    g.FillPolygon(brush, speaker);
                    if (active)
                    {
                        g.DrawLine(pen, cx + 3.5f, cy - 4.5f, cx + 8.5f, cy + 4.5f);
                        g.DrawLine(pen, cx + 8.5f, cy - 4.5f, cx + 3.5f, cy + 4.5f);
                    }
                    else
                    {
                        g.DrawArc(pen, cx - 1f, cy - 4.5f, 9f, 9f, -55f, 110f);
                    }
                }
            }
        }
    }
}
