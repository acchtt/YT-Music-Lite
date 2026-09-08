using System;
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
