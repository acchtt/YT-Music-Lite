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
                using (YTMusicLite.SettingsForm settings = new YTMusicLite.SettingsForm(form))
                {
                }
                Console.WriteLine("MainForm and SettingsForm constructor smoke test passed.");
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
