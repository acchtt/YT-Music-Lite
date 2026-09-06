using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using YTMusicLite;

namespace YTMusicLiteSmoke
{
    static class SmokeTest
    {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Raise(Control control, string method, object args)
        {
            control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { args });
        }
        static T Field<T>(object obj, string name)
        {
            return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        }
        static void Capture(Form form, string name)
        {
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(name);
            }
        }

        static async Task CheckUpdatePreparation()
        {
            string source = Path.GetTempFileName();
            string version = "ui-test-" + Guid.NewGuid().ToString("N");
            string preparedPath = Path.Combine(Path.GetTempPath(), "YTMusicLiteUpdate", "YTMusicLite-" + version + ".zip");
            try
            {
                File.WriteAllText(source, "local update verification fixture");
                string digest;
                using (SHA256 sha = SHA256.Create())
                    digest = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-", "").ToLowerInvariant();
                UpdateService service = new UpdateService();
                UpdateCheckResult update = new UpdateCheckResult { UpdateAvailable = true, Version = version, DownloadUrl = new Uri(source).AbsoluteUri, Sha256 = digest };
                PreparedUpdate prepared = await service.PrepareAsync(update, null);
                Assert(File.Exists(prepared.ZipPath), "A verified download must be prepared without installing");
                File.AppendAllText(prepared.ZipPath, "changed after verification");
                bool rejected = false;
                try { service.InstallPrepared(prepared); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected, "Changed downloads must be rejected before launching the installer");
                update.Sha256 = new string('0', 64);
                rejected = false;
                try { await service.PrepareAsync(update, null); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected && !File.Exists(preparedPath), "Bad checksums must reject and remove the download");
                update.Sha256 = "";
                rejected = false;
                try { await service.PrepareAsync(update, null); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected && !File.Exists(preparedPath), "Missing checksums must reject and remove the download");
            }
            finally
            {
                File.Delete(source);
                if (File.Exists(preparedPath)) File.Delete(preparedPath);
            }
        }

        [STAThread]
        static int Main()
        {
            try
            {
                Task.Run((Func<Task>)CheckUpdatePreparation).GetAwaiter().GetResult();
                using (Bitmap sheet = new Bitmap(576, 384))
                using (Graphics g = Graphics.FromImage(sheet))
                using (Font label = new Font("Segoe UI", 8f))
                {
                    g.Clear(BrandArt.Surface);
                    int index = 0;
                    foreach (IconKind kind in Enum.GetValues(typeof(IconKind)))
                    {
                        int x = index % 6 * 96, y = index / 6 * 96;
                        IconArt.Draw(g, kind, new Rectangle(x + 32, y + 14, 32, 32), Color.White, 1.8f);
                        g.DrawString(kind.ToString(), label, Brushes.LightGray, x + 12, y + 60);
                        index++;
                    }
                    sheet.Save("icon-set.png");
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (Form host = new Form())
                using (SeekBar slider = new SeekBar { Width = 208, Duration = 100, Ratio = 0.5 })
                {
                    host.Controls.Add(slider);
                    host.Show();
                    int commits = 0;
                    slider.SeekRequested += delegate { commits++; };
                    Raise(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
                    Assert(Math.Abs(slider.Ratio - 0.55) < 0.001 && commits == 1, "Arrow keys must seek five seconds");
                    Raise(slider, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 44, 10, 0));
                    Raise(slider, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 164, 10, 0));
                    slider.Ratio = 0.1;
                    Assert(Math.Abs(slider.Ratio - 0.8) < 0.001, "Playback polling must not overwrite a drag");
                    Raise(slider, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 164, 10, 0));
                    Assert(commits == 2 && !slider.IsDragging, "Dragging commits once on release");
                    slider.Interactive = false;
                    Raise(slider, "OnKeyDown", new KeyEventArgs(Keys.Home));
                    Assert(commits == 2 && !slider.TabStop, "Unavailable seek must not activate");
                    slider.Interactive = true;
                    slider.Ratio = double.NaN;
                    Assert(slider.Ratio == 0, "Invalid values must not reach painting");
                    Assert(slider.AccessibilityObject.Role == AccessibleRole.Slider, "Expose slider accessibility role");
                    Assert(SeekBar.FormatTime(3661) == "1:01:01", "Long tracks need hours");
                }
                Assert(MiniPlayerForm.ClampToWorkingArea(new Point(-5000, 2000), new Size(460, 185), new Rectangle(-1920, 0, 1920, 1080)) == new Point(-1920, 895), "Restore bounds must stay on a negative-coordinate monitor");
                using (MainForm main = (MainForm)Activator.CreateInstance(typeof(MainForm), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { false }, null))
                {
                    UiPolish.Attach(main);
                    OfficialPlayerMode.Attach(main);
                    Assert(!Field<Panel>(main, "playerBar").Visible, "Keep the official player active");
                    LiteButton back = Field<LiteButton>(main, "backButton");
                    Assert(back.DrawnIcon == IconKind.Back && back.TabStop && !back.Enabled, "Back button needs an icon, keyboard access and history state");
                    Assert(Field<ToolTip>(main, "tips").GetToolTip(back) == "Back", "Styling must preserve tooltips");
                    main.Show();
                    MethodInfo showState = typeof(MainForm).GetMethod("ShowState", BindingFlags.Instance | BindingFlags.NonPublic);
                    bool recovered = false;
                    showState.Invoke(main, new object[] { "Taking a break", "Music paused to save resources.", "Resume", new Action(delegate { recovered = true; }) });
                    Application.DoEvents();
                    Capture(main, "sleep-screen.png");
                    Assert(Field<Panel>(main, "statePanel").Visible, "Sleep recovery must be visible");
                    Field<LiteButton>(main, "stateAction").PerformClick();
                    Assert(recovered, "Recovery action must be operable");
                    showState.Invoke(main, new object[] { "Couldn’t load your music", "Check your connection, then try again.", "Retry", new Action(delegate { }) });
                    Application.DoEvents();
                    Capture(main, "retry-screen.png");
                    main.Hide();
                    MiniPlayerForm mini = Field<MiniPlayerForm>(main, "miniPlayer");
                    mini.UpdatePlayer(new PlayerState { Title = "A long track title for layout verification", Artist = "Artist name", Duration = 240, CurrentTime = 61, Volume = 0.5, Paused = false });
                    LiteButton play = Field<LiteButton>(mini, "playPause");
                    Assert(play.DrawnIcon == IconKind.Pause && play.AccessibleName == "Pause", "Play state must update icon and accessible name");
                    Assert(Field<ToolTip>(mini, "tips").GetToolTip(play) == "Pause", "Play state must update tooltip");
                    mini.Show();
                    Application.DoEvents();
                    Capture(mini, "mini-player.png");
                    mini.Hide();
                    foreach (float scale in new float[] { 1f, 1.25f, 1.5f, 2f })
                    {
                        using (SettingsForm settings = new SettingsForm(main))
                        {
                            settings.Show();
                            settings.Scale(new SizeF(scale, scale));
                            settings.PerformLayout();
                            Application.DoEvents();
                            Capture(settings, "settings-" + (int)(scale * 100) + ".png");
                            settings.ShowUpdates();
                            Application.DoEvents();
                            Capture(settings, "updates-" + (int)(scale * 100) + ".png");
                        }
                    }
                }
                Console.WriteLine("PASS: update preparation and tamper rejection, native forms, official player, control semantics, seeking, mini placement, tooltips, and scaled Settings captures.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
