using System;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace YTMusicLite.Client
{
    internal static class App
    {
        [STAThread]
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (args != null && args.Length >= 2 && string.Equals(args[0], "--catalog-check", StringComparison.OrdinalIgnoreCase))
            {
                string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalog-check.txt");
                try
                {
                    using (CatalogService catalog = new CatalogService())
                    {
                        System.Collections.Generic.List<Track> tracks = catalog.SearchAsync(args[1]).GetAwaiter().GetResult();
                        if (tracks.Count == 0) throw new InvalidOperationException("Search returned no songs.");
                        File.WriteAllText(report, "PASS: " + tracks.Count + " YouTube results; first result: " + tracks[0].Title);
                        return 0;
                    }
                }
                catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); return 2; }
            }
            if (args != null && args.Length >= 2 && string.Equals(args[0], "--youtube-playback-check", StringComparison.OrdinalIgnoreCase))
            {
                string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "youtube-playback-check.txt");
                try
                {
                    using (CatalogService catalog = new CatalogService())
                    using (PlaybackEngine playback = new PlaybackEngine())
                    {
                        System.Collections.Generic.List<Track> tracks = catalog.SearchAsync(args[1]).GetAwaiter().GetResult();
                        if (tracks.Count == 0) throw new InvalidOperationException("Search returned no songs.");
                        playback.PlayAsync(tracks[0], true).GetAwaiter().GetResult();
                        System.Threading.Thread.Sleep(5000);
                        if (playback.State != PlaybackState.Playing && playback.State != PlaybackState.Paused) throw new InvalidOperationException("The resolved YouTube stream did not remain active.");
                        playback.Stop();
                        File.WriteAllText(report, "PASS: searched, resolved, and streamed: " + tracks[0].Title);
                        return 0;
                    }
                }
                catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); return 3; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                try { MessageBox.Show(e.Exception.Message, "YT Music Lite", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            };
            Application.Run(new MainWindow(args ?? new string[0]));
            return Environment.ExitCode;
        }
    }
}
