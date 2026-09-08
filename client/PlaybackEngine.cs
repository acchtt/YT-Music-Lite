using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace YTMusicLite.Client
{
    internal sealed class PlaybackEngine : IDisposable
    {
        private readonly object gate = new object();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private Process resolver;
        private Process player;
        private string pipeName;
        private Track current;
        private int generation;
        private int volume = 85;
        private Timer pollTimer;
        private int polling;
        private bool disposed;

        public event EventHandler<PlaybackSnapshot> SnapshotChanged;
        public event EventHandler PlaybackEnded;

        public PlaybackState State { get; private set; }
        public Track CurrentTrack { get { return current; } }

        public PlaybackEngine()
        {
            State = PlaybackState.Stopped;
            pollTimer = new Timer(Poll, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task PlayAsync(Track track, bool silentOutput)
        {
            if (track == null || string.IsNullOrWhiteSpace(track.Source)) throw new ArgumentException("The selected song has no playable source.");
            int request;
            lock (gate)
            {
                StopProcesses();
                generation++;
                request = generation;
                current = track;
                State = PlaybackState.Resolving;
            }
            Raise(new PlaybackSnapshot { State = PlaybackState.Resolving, Track = track, Volume = volume });

            try
            {
                string source = track.Source;
                if (!File.Exists(source)) source = await ResolveAsync(source, request);
                if (request != generation) return;
                string localPipe = "ytmlite-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N");
                string arguments = "--input-ipc-server=" + localPipe + " --no-config --no-video --ytdl=no --terminal=no --idle=no --keep-open=no --cache=yes --demuxer-max-bytes=8MiB --demuxer-max-back-bytes=0 --cache-secs=8 --volume=" + volume.ToString(CultureInfo.InvariantCulture) + " ";
                if (silentOutput) arguments += "--ao=null ";
                arguments += "-- " + ProcessTools.Quote(source);
                Process launched = ProcessTools.Start(ProcessTools.Find("mpv"), arguments, false);
                lock (gate)
                {
                    if (request != generation) { ProcessTools.KillTree(launched); launched.Dispose(); return; }
                    player = launched;
                    pipeName = localPipe;
                    State = PlaybackState.Playing;
                }
                Raise(new PlaybackSnapshot { State = PlaybackState.Playing, Track = track, Volume = volume, DurationSeconds = track.DurationSeconds });
                pollTimer.Change(500, 650);
                WatchPlayer(launched, request);
            }
            catch (Exception error)
            {
                if (request != generation) return;
                lock (gate) { State = PlaybackState.Failed; StopProcesses(); }
                Raise(new PlaybackSnapshot { State = PlaybackState.Failed, Track = track, Volume = volume, Error = error.Message });
                throw;
            }
        }

        public async Task TogglePauseAsync()
        {
            if (!IsPlayerAlive()) return;
            await SendAsync(new object[] { "cycle", "pause" });
            await PollNow();
        }

        public async Task SeekAsync(double seconds)
        {
            if (!IsPlayerAlive()) return;
            await SendAsync(new object[] { "set_property", "time-pos", Math.Max(0, seconds) });
            await PollNow();
        }

        public async Task SetVolumeAsync(int value)
        {
            volume = Math.Max(0, Math.Min(100, value));
            if (IsPlayerAlive()) await SendAsync(new object[] { "set_property", "volume", volume });
        }

        public void Stop()
        {
            lock (gate)
            {
                generation++;
                StopProcesses();
                State = PlaybackState.Stopped;
                current = null;
            }
            Raise(new PlaybackSnapshot { State = PlaybackState.Stopped, Volume = volume });
        }

        private async Task<string> ResolveAsync(string source, int request)
        {
            string runtime = ProcessTools.Find("deno");
            string arguments = "--ignore-config --js-runtimes " + ProcessTools.Quote("deno:" + runtime) + " --no-playlist --no-warnings --socket-timeout 15 --retries 1 -f bestaudio --get-url -- " + ProcessTools.Quote(source);
            Process process = ProcessTools.Start(ProcessTools.Find("yt-dlp"), arguments, true);
            lock (gate) resolver = process;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            Task exited = Task.Run(delegate { process.WaitForExit(); });
            if (await Task.WhenAny(exited, Task.Delay(60000)) != exited)
            {
                ProcessTools.KillTree(process);
                throw new TimeoutException("YouTube took too long to prepare this song.");
            }
            await exited;
            string url = (await output).Trim().Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
            string error = await errors;
            lock (gate) if (resolver == process) resolver = null;
            if (request != generation) throw new OperationCanceledException();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("This song could not be prepared. " + Short(error));
            process.Dispose();
            return url;
        }

        private async void WatchPlayer(Process process, int request)
        {
            try
            {
                await Task.Run(delegate { process.WaitForExit(); });
                if (request != generation || disposed) return;
                bool successful = process.ExitCode == 0;
                lock (gate)
                {
                    if (player == process) { player = null; pipeName = null; }
                    State = successful ? PlaybackState.Stopped : PlaybackState.Failed;
                }
                pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
                if (successful)
                {
                    Raise(new PlaybackSnapshot { State = PlaybackState.Stopped, Track = current, PositionSeconds = current == null ? 0 : current.DurationSeconds, DurationSeconds = current == null ? 0 : current.DurationSeconds, Volume = volume });
                    EventHandler handler = PlaybackEnded;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
                else Raise(new PlaybackSnapshot { State = PlaybackState.Failed, Track = current, Volume = volume, Error = "Playback stopped unexpectedly." });
            }
            catch { }
        }

        private async void Poll(object ignored)
        {
            if (Interlocked.Exchange(ref polling, 1) != 0) return;
            try { await PollNow(); }
            catch { }
            finally { Interlocked.Exchange(ref polling, 0); }
        }

        private async Task PollNow()
        {
            if (!IsPlayerAlive()) return;
            object position = await GetPropertyAsync("time-pos");
            object duration = await GetPropertyAsync("duration");
            object paused = await GetPropertyAsync("pause");
            double positionNumber = ToDouble(position);
            double durationNumber = ToDouble(duration);
            bool isPaused = paused is bool && (bool)paused;
            State = isPaused ? PlaybackState.Paused : PlaybackState.Playing;
            Raise(new PlaybackSnapshot { State = State, Track = current, PositionSeconds = positionNumber, DurationSeconds = durationNumber, Volume = volume });
        }

        private async Task<object> GetPropertyAsync(string name)
        {
            Dictionary<string, object> response = await SendAsync(new object[] { "get_property", name });
            object value;
            return response.TryGetValue("data", out value) ? value : null;
        }

        private async Task<Dictionary<string, object>> SendAsync(object[] command)
        {
            string pipe;
            lock (gate) pipe = pipeName;
            if (string.IsNullOrEmpty(pipe)) throw new InvalidOperationException("Nothing is playing.");
            using (NamedPipeClientStream stream = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await Task.Run(delegate { stream.Connect(2200); });
                string payload = serializer.Serialize(new Dictionary<string, object> { { "command", command } }) + "\n";
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                {
                    Task<string> read = reader.ReadLineAsync();
                    if (await Task.WhenAny(read, Task.Delay(2200)) != read) throw new TimeoutException("The audio player did not respond.");
                    string line = await read;
                    if (string.IsNullOrWhiteSpace(line)) throw new IOException("The audio player closed its control channel.");
                    Dictionary<string, object> result = serializer.Deserialize<Dictionary<string, object>>(line);
                    object error;
                    if (result.TryGetValue("error", out error) && !string.Equals(Convert.ToString(error), "success", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Player command failed: " + error);
                    return result;
                }
            }
        }

        private bool IsPlayerAlive()
        {
            lock (gate)
            {
                try { return player != null && !player.HasExited; }
                catch { return false; }
            }
        }

        private void StopProcesses()
        {
            pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (resolver != null) { ProcessTools.KillTree(resolver); resolver.Dispose(); resolver = null; }
            if (player != null) { ProcessTools.KillTree(player); player.Dispose(); player = null; }
            pipeName = null;
        }

        private static string Short(string text)
        {
            text = (text ?? "").Trim();
            return text.Length <= 180 ? text : text.Substring(0, 180);
        }

        private static double ToDouble(object value)
        {
            if (value == null) return 0;
            double number;
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
        }

        private void Raise(PlaybackSnapshot snapshot)
        {
            EventHandler<PlaybackSnapshot> handler = SnapshotChanged;
            if (handler != null) handler(this, snapshot);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
            pollTimer.Dispose();
        }
    }
}
