using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal static class ArtworkCache
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<string, Image> memory = new Dictionary<string, Image>();
        private static readonly HashSet<string> loading = new HashSet<string>();
        private static readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YTMusicLite", "artwork");
        private const int MemoryLimit = 36;

        public static Image Get(string url, Action loaded)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            lock (gate)
            {
                Image cached;
                if (memory.TryGetValue(url, out cached)) return cached;
                if (loading.Contains(url)) return null;
                loading.Add(url);
            }
            Task.Run(delegate { Load(url, loaded); });
            return null;
        }

        private static void Load(string url, Action loaded)
        {
            Image image = null;
            try
            {
                Directory.CreateDirectory(root);
                string path = Path.Combine(root, Hash(url) + ".jpg");
                if (!File.Exists(path))
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.UserAgent] = "YTMusicLite/7.1";
                        client.DownloadFile(url, path + ".tmp");
                    }
                    File.Move(path + ".tmp", path);
                }
                using (FileStream stream = File.OpenRead(path))
                using (Image source = Image.FromStream(stream)) image = new Bitmap(source, new Size(160, 160));
            }
            catch { image = null; }
            lock (gate)
            {
                loading.Remove(url);
                if (image != null)
                {
                    if (memory.Count >= MemoryLimit)
                    {
                        string removeKey = null;
                        foreach (KeyValuePair<string, Image> item in memory) { removeKey = item.Key; break; }
                        if (removeKey != null) { memory[removeKey].Dispose(); memory.Remove(removeKey); }
                    }
                    memory[url] = image;
                }
            }
            if (loaded != null) loaded();
        }

        private static string Hash(string value)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder text = new StringBuilder();
                foreach (byte item in bytes) text.Append(item.ToString("x2"));
                return text.ToString();
            }
        }
    }

    internal sealed class ArtworkControl : Control
    {
        private string url;
        private string keyText;
        public string ArtworkUrl { get { return url; } set { url = value; Invalidate(); } }
        public string KeyText { get { return keyText; } set { keyText = value; Invalidate(); } }
        public int Radius { get; set; }

        public ArtworkControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Radius = 8;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.EnableQuality(e.Graphics);
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(bounds, Radius))
            {
                e.Graphics.SetClip(path);
                Image image = ArtworkCache.Get(url, delegate { try { if (IsHandleCreated) BeginInvoke((Action)Invalidate); } catch { } });
                if (image != null) e.Graphics.DrawImage(image, bounds);
                else DrawPlaceholder(e.Graphics, bounds, keyText);
                e.Graphics.ResetClip();
            }
        }

        internal static void DrawPlaceholder(Graphics graphics, Rectangle bounds, string text)
        {
            int hash = (text ?? "YT").GetHashCode();
            Color first = Color.FromArgb(48 + Math.Abs(hash % 90), 43 + Math.Abs((hash >> 5) % 80), 92 + Math.Abs((hash >> 11) % 90));
            Color second = Theme.Accent;
            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, first, second, 45f)) graphics.FillRectangle(brush, bounds);
            string initial = string.IsNullOrWhiteSpace(text) ? "♪" : text.Trim().Substring(0, 1).ToUpperInvariant();
            using (Font font = new Font("Segoe UI", Math.Max(10, bounds.Height * .32f), FontStyle.Bold)) TextRenderer.DrawText(graphics, initial, font, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
