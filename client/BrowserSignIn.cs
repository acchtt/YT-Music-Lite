using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace YTMusicLite.Client
{
    internal static class BrowserSignIn
    {
        internal const string AccountChooserUrl = "https://accounts.google.com/AccountChooser?service=youtube&continue=https%3A%2F%2Fmusic.youtube.com%2F";

        public static string Open(string browser)
        {
            string executable = FindExecutable(browser);
            if (string.IsNullOrEmpty(executable))
            {
                return DisplayName(browser) + " was not found on this PC. Install it or choose another browser.";
            }

            try
            {
                Process process = Process.Start(new ProcessStartInfo(executable, Arguments(browser))
                {
                    UseShellExecute = true
                });
                if (process == null) return "Windows could not open " + DisplayName(browser) + ".";
                return null;
            }
            catch (Exception error)
            {
                return "Could not open " + DisplayName(browser) + ": " + error.Message;
            }
        }

        internal static string Arguments(string browser)
        {
            string newWindow = string.Equals(browser, "firefox", StringComparison.OrdinalIgnoreCase) ? "-new-window " : "--new-window ";
            return newWindow + ProcessTools.Quote(AccountChooserUrl);
        }

        private static string FindExecutable(string browser)
        {
            List<string> candidates = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.Equals(browser, "edge", StringComparison.OrdinalIgnoreCase))
            {
                Add(candidates, programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe");
                Add(candidates, programFiles, "Microsoft", "Edge", "Application", "msedge.exe");
                Add(candidates, local, "Microsoft", "Edge", "Application", "msedge.exe");
            }
            else if (string.Equals(browser, "chrome", StringComparison.OrdinalIgnoreCase))
            {
                Add(candidates, programFiles, "Google", "Chrome", "Application", "chrome.exe");
                Add(candidates, programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
                Add(candidates, local, "Google", "Chrome", "Application", "chrome.exe");
            }
            else if (string.Equals(browser, "firefox", StringComparison.OrdinalIgnoreCase))
            {
                Add(candidates, programFiles, "Mozilla Firefox", "firefox.exe");
                Add(candidates, programFilesX86, "Mozilla Firefox", "firefox.exe");
                Add(candidates, local, "Mozilla Firefox", "firefox.exe");
            }

            foreach (string candidate in candidates) if (File.Exists(candidate)) return candidate;
            return null;
        }

        private static void Add(List<string> candidates, params string[] parts)
        {
            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0])) return;
            candidates.Add(Path.Combine(parts));
        }

        private static string DisplayName(string browser)
        {
            if (string.Equals(browser, "edge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
            if (string.Equals(browser, "chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
            if (string.Equals(browser, "firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
            return "the selected browser";
        }
    }
}
