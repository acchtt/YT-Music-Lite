using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Windows.Forms;

// Deliberately independent of WebView2 and the released application.
internal sealed partial class NativePlayer : Form
{
    readonly TextBox input = new TextBox { Dock = DockStyle.Fill };
    readonly Label status = new Label { Dock = DockStyle.Fill, Text = "Paste a YouTube link or open an audio file.", AutoEllipsis = true };
    readonly Button play = new Button { Text = "Play", AutoSize = true };
    readonly Button pause = new Button { Text = "Pause / resume", AutoSize = true };
    readonly Button stop = new Button { Text = "Stop", AutoSize = true };
    Process resolver, player;
    string pipeName;
    bool busy, closing;
    int generation;
    readonly bool benchmark;
    readonly Timer automation = new Timer { Interval = 1000 };
    int tick;

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new NativePlayer(args));
        return 0;
    }

    NativePlayer(string[] args)
    {
        bool uiCheck = args.Length == 1 && args[0] == "--ui-check";
        benchmark = uiCheck || (args.Length == 2 && args[0] == "--benchmark");
        BuildMusicUi();
        play.Click += async delegate { await PlayAsync(); };
        pause.Click += async delegate {
            try { await CommandAsync("cycle pause"); status.Text = "Pause toggled"; }
            catch (Exception e) { status.Text = e.Message; }
        };
        stop.Click += delegate { queueIndex = -1; Stop(); status.Text = "Stopped — playback processes released"; };
        FormClosing += delegate { closing = true; automation.Stop(); if (searchProcess != null) KillTree(searchProcess); if (mini != null) mini.Close(); Stop(); };
        if (uiCheck) Shown += delegate { RunUiCheck(); };
        if (benchmark && !uiCheck)
        {
            input.Text = args[1];
            automation.Tick += async delegate {
                tick++;
                if (tick == 3) await PlayAsync();
                if (tick == 8) {
                    try {
                        string position = await CommandAsync("get_property time-pos");
                        if (!position.Contains("success")) throw new Exception("Playback did not start");
                        File.AppendAllText("benchmark-stages.txt", "playing " + position + Environment.NewLine);
                    } catch (Exception e) { File.AppendAllText("benchmark-stages.txt", "FAIL " + e.Message + Environment.NewLine); }
                }
                if (tick == 10) {
                    try {
                        await CommandAsync("set pause yes");
                        string state = await CommandAsync("get_property pause");
                        File.AppendAllText("benchmark-stages.txt", "paused " + state + Environment.NewLine);
                    } catch (Exception e) { File.AppendAllText("benchmark-stages.txt", "FAIL " + e.Message + Environment.NewLine); }
                }
                if (tick == 16) stop.PerformClick();
                if (tick == 21) Close();
            };
            Shown += delegate { automation.Start(); };
        }
    }

    static string FindTool(string name)
    {
        string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".exe");
        if (File.Exists(local)) return local;
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try { string path = Path.Combine(dir.Trim('"'), name + ".exe"); if (File.Exists(path)) return path; }
            catch (ArgumentException) { }
        }
        throw new InvalidOperationException("Install " + name + " or place its executable beside this app. See the prototype README.");
    }

    // Windows CommandLineToArgvW escaping; no shell is used for supplied URLs or paths.
    static string Quote(string value)
    {
        var result = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c); slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }

    static Process Start(string executable, string arguments, bool output)
    {
        var process = new Process { StartInfo = new ProcessStartInfo(executable, arguments) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = !output, RedirectStandardOutput = output, RedirectStandardError = output
        }};
        process.Start(); return process;
    }

    async Task PlayAsync()
    {
        if (busy) return;
        Stop(); int request = generation; busy = true; play.Enabled = false;
        try
        {
            string source = input.Text.Trim(); string mpv = FindTool("mpv");
            if (!File.Exists(source))
            {
                Uri uri;
                if (!Uri.TryCreate(source, UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                    !(uri.Host == "youtu.be" || uri.Host == "youtube.com" || uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Enter an HTTPS YouTube link or an existing audio file.");
                status.Text = "Resolving audio…";
                var process = Start(FindTool("yt-dlp"), "--ignore-config --no-playlist --no-warnings --socket-timeout 15 --retries 1 -f bestaudio --get-url -- " + Quote(source), true);
                resolver = process;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                var exited = Task.Run(delegate { process.WaitForExit(); });
                if (await Task.WhenAny(exited, Task.Delay(60000)) != exited) { KillTree(process); throw new TimeoutException("Audio lookup timed out. Try again."); }
                await exited;
                string output = await stdout; string error = await stderr;
                if (request != generation || closing) return;
                if (process.ExitCode != 0) throw new InvalidOperationException("YouTube lookup failed. Update yt-dlp and its JS runtime, then retry. " + error.Substring(0, Math.Min(error.Length, 220)));
                source = output.Trim();
                Uri stream;
                if (!Uri.TryCreate(source, UriKind.Absolute, out stream) || stream.Scheme != "https")
                    throw new InvalidOperationException("The resolver did not return one HTTPS audio stream.");
                process.Dispose(); resolver = null;
            }
            if (request != generation || closing) return;
            pipeName = "ytml-native-" + Guid.NewGuid().ToString("N");
            player = Start(mpv, "--input-ipc-server=" + pipeName + " --no-config --no-video --ytdl=no --terminal=no --cache=yes --demuxer-max-bytes=8MiB --demuxer-max-back-bytes=0 --cache-secs=10 " + (benchmark ? "--ao=null " : "") + "-- " + Quote(source), false);
            status.Text = "Playing";
            WatchPlayer(player, request);
        }
        catch (Exception e) { if (!closing && request == generation) status.Text = e.Message; }
        finally { if (resolver != null) { KillTree(resolver); resolver.Dispose(); resolver = null; } busy = false; if (!closing) play.Enabled = true; }
    }

    async Task<string> CommandAsync(string command)
    {
        if (player == null || player.HasExited) throw new InvalidOperationException("No active player");
        string name = pipeName;
        using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await Task.Run(delegate { pipe.Connect(2000); });
            using (var writer = new StreamWriter(pipe, new System.Text.UTF8Encoding(false), 1024, true))
            using (var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, true, 1024, true))
            {
                writer.AutoFlush = true;
                // Only internal, fixed command tokens reach this method.
                string message = "{\"command\":[" + string.Join(",", command.Split(' ').Select(x => "\"" + x + "\"")) + "],\"request_id\":1}";
                await writer.WriteLineAsync(message);
                for (int i = 0; i < 30; i++)
                {
                    var line = reader.ReadLineAsync();
                    if (await Task.WhenAny(line, Task.Delay(2000)) != line) throw new TimeoutException("Player response timed out");
                    string response = await line;
                    if (response == null) throw new IOException("Player disconnected");
                    if (response.Contains("\"error\"")) return response;
                }
                throw new IOException("No player response");
            }
        }
    }

    static void KillTree(Process process)
    {
        try {
            if (!process.HasExited)
            {
                using (var killer = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true }))
                    killer.WaitForExit(3000);
            }
        } catch (InvalidOperationException) { }
    }
    void Stop()
    {
        generation++;
        if (resolver != null) KillTree(resolver);
        if (player != null) { KillTree(player); player.Dispose(); player = null; }
    }
    protected override void Dispose(bool disposing) { if (disposing) automation.Dispose(); base.Dispose(disposing); }
}
