using System;
using System.Net;
using System.Windows.Forms;

namespace YTMusicLite
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // GitHub requires TLS 1.2+. Older .NET Framework defaults can try TLS 1.0
            // and fail with "Could not create SSL/TLS secure channel".
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
