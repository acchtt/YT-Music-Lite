using System;
using System.Windows.Forms;

namespace YTMusicLiteSmoke
{
    static class SmokeTest
    {
        [STAThread]
        static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (YTMusicLite.MainForm form = new YTMusicLite.MainForm())
                {
                }
                Console.WriteLine("MainForm constructor smoke test passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
