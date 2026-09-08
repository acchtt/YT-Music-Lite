using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace YTMusicLite.Client
{
    internal static class ProcessTools
    {
        public static string Find(string name)
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".exe");
            if (File.Exists(local)) return local;
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    string path = Path.Combine(directory.Trim('"'), name + ".exe");
                    if (File.Exists(path)) return path;
                }
                catch (ArgumentException) { }
            }
            throw new FileNotFoundException(name + ".exe is missing. Reinstall YT Music Lite and try again.");
        }

        public static string Quote(string value)
        {
            StringBuilder result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value ?? "")
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }

        public static Process Start(string executable, string arguments, bool captureOutput)
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(executable, arguments);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = captureOutput;
            process.StartInfo.RedirectStandardError = captureOutput;
            process.StartInfo.RedirectStandardInput = !captureOutput;
            process.StartInfo.EnvironmentVariables["PATH"] = AppDomain.CurrentDomain.BaseDirectory + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
            process.Start();
            return process;
        }

        public static void KillTree(Process process)
        {
            if (process == null) return;
            try
            {
                if (process.HasExited) return;
                using (Process killer = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true }))
                {
                    if (killer != null) killer.WaitForExit(3000);
                }
            }
            catch { try { process.Kill(); } catch { } }
        }
    }
}
