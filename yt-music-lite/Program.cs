using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace YTMusicLite
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                MainForm main = new MainForm();
                WebViewChromeFix.Attach(main);
                Application.Run(main);
            }
            catch (Exception ex)
            {
                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "YTMusicLite");
                    Directory.CreateDirectory(dir);
                    string log = Path.Combine(dir, "startup-crash.log");
                    File.WriteAllText(log, DateTime.Now.ToString("s") + Environment.NewLine + ex.ToString(), Encoding.UTF8);
                }
                catch
                {
                }

                try
                {
                    MessageBox.Show(
                        "YT Music Lite could not start. A crash log was saved to %LOCALAPPDATA%\\YTMusicLite\\startup-crash.log.\r\n\r\n" + ex.Message,
                        "YT Music Lite",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                }
            }
        }
    }
}
