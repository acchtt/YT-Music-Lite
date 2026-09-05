using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace YTMusicLiteUpdater
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args == null || args.Length < 4) return 2;

            string zipPath = args[0];
            string appDir = args[1];
            int parentPid;
            if (!int.TryParse(args[2], out parentPid)) return 3;
            string restartExe = args[3];

            try
            {
                WaitForProcessExit(parentPid);
                ApplyUpdate(zipPath, appDir);
                try { File.Delete(zipPath); } catch { }

                if (File.Exists(restartExe))
                {
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = restartExe;
                    info.WorkingDirectory = appDir;
                    info.UseShellExecute = true;
                    Process.Start(info);
                }
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "YTMusicLite-update-error.txt"), ex.ToString());
                }
                catch { }
                return 1;
            }
        }

        private static void WaitForProcessExit(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                process.WaitForExit(30000);
            }
            catch
            {
            }
            Thread.Sleep(500);
        }

        private static void ApplyUpdate(string zipPath, string appDir)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Update ZIP not found.", zipPath);
            if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir)) throw new DirectoryNotFoundException("Application directory not found.");

            string staging = Path.Combine(Path.GetTempPath(), "YTMusicLiteUpdate", "staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(zipPath, staging);

            string sourceRoot = ResolvePayloadRoot(staging);
            CopyDirectory(sourceRoot, appDir);

            try { Directory.Delete(staging, true); } catch { }
        }

        private static string ResolvePayloadRoot(string staging)
        {
            string[] files = Directory.GetFiles(staging);
            string[] dirs = Directory.GetDirectories(staging);
            if (files.Length == 0 && dirs.Length == 1)
            {
                string nested = dirs[0];
                if (File.Exists(Path.Combine(nested, "YTMusicLite.exe"))) return nested;
            }
            return staging;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                string target = Path.Combine(destination, name);
                CopyWithRetry(files[i], target);
            }

            string[] dirs = Directory.GetDirectories(source);
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (string.Equals(name, "WebView2Profile", StringComparison.OrdinalIgnoreCase)) continue;
                CopyDirectory(dirs[i], Path.Combine(destination, name));
            }
        }

        private static void CopyWithRetry(string source, string destination)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    File.Copy(source, destination, true);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(250);
                }
            }
            throw new IOException("Could not replace " + destination, last);
        }
    }
}
