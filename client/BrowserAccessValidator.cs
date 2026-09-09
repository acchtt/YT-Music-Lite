using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YTMusicLite.Client
{
    internal static class BrowserAccessValidator
    {
        public static async Task<string> ValidateAsync(ClientSettings settings)
        {
            string temporary = Path.Combine(Path.GetTempPath(), "ytmlite-auth-" + Guid.NewGuid().ToString("N") + ".txt");
            Process process = null;
            try
            {
                string arguments = "--ignore-config" + YtDlpOptions.Authentication(settings) + " --cookies " + ProcessTools.Quote(temporary) + " --simulate --skip-download --no-warnings --socket-timeout 15 --retries 0 -- " + ProcessTools.Quote("https://www.youtube.com/watch?v=jNQXAC9IVRw");
                process = ProcessTools.Start(ProcessTools.Find("yt-dlp"), arguments, true);
                Task<string> errors = process.StandardError.ReadToEndAsync();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task exited = Task.Run(delegate { process.WaitForExit(); });
                if (await Task.WhenAny(exited, Task.Delay(45000)) != exited)
                {
                    ProcessTools.KillTree(process);
                    return "Sign-in verification timed out. Close Brave completely and try again.";
                }
                await exited;
                await output;
                string error = await errors;
                if (!File.Exists(temporary)) return "No Brave session was found. Finish Google sign-in, close Brave, and try again.";
                string cookies = File.ReadAllText(temporary);
                if (HasAccountCookie(cookies)) return null;
                if (!string.IsNullOrWhiteSpace(error) && process.ExitCode != 0) return "Brave session could not be read. Close every Brave window and try again.";
                return "Google sign-in was not detected in the YT Music Lite Brave window. Please sign in and try again.";
            }
            catch (Exception error)
            {
                return "Could not verify Brave sign-in: " + error.Message;
            }
            finally
            {
                if (process != null) process.Dispose();
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }

        internal static bool HasAccountCookie(string cookies)
        {
            string value = cookies ?? "";
            return value.IndexOf("\tSAPISID\t", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("\t__Secure-1PAPISID\t", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("\t__Secure-3PAPISID\t", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("\tLOGIN_INFO\t", StringComparison.Ordinal) >= 0;
        }
    }
}
