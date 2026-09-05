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
                    YTMusicLite.UiPolish.Attach(form);
                    form.CreateControl();
                    form.PerformLayout();
                    using (YTMusicLite.SettingsForm settings = new YTMusicLite.SettingsForm(form))
                    {
                        settings.CreateControl();
                        settings.PerformLayout();
                    }
                }
                Console.WriteLine("MainForm, icon skin, and SettingsForm smoke test passed.");
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
