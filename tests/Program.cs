using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MemoryOrb;

internal class Program
{
    private static int count;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); count++;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            var gate = new OptimizationGate();
            Check(!gate.ShouldRun(false, false, 99, now), "disabled switch blocks auto cleaning");
            Check(!gate.ShouldRun(true, false, 80, now), "wait for sustained pressure at threshold");
            bool premature = false;
            for (int second = 2; second < 30; second += 2) premature |= gate.ShouldRun(true, false, 80, now.AddSeconds(second));
            Check(!premature, "no premature auto cleaning");
            Check(gate.ShouldRun(true, false, 90, now.AddSeconds(30)), "30-second pressure trigger");
            gate.RecordAttempt(now.AddSeconds(30));
            Check(!gate.ShouldRun(true, false, 90, now.AddMinutes(1)), "cooldown starts");
            bool cooldownTriggered = false;
            for (int second = 62; second < 630; second += 2) cooldownTriggered |= gate.ShouldRun(true, false, 90, now.AddSeconds(second));
            Check(!cooldownTriggered, "cooldown remains active despite sustained pressure");
            Check(gate.ShouldRun(true, false, 90, now.AddSeconds(630)), "cooldown expires at configured boundary");
            Check(!gate.ShouldRun(true, true, 90, now.AddSeconds(632)), "busy prevents auto cleaning");
            Check(!gate.ShouldRun(true, false, 40, now.AddMinutes(12)), "low load resets pressure");
            Check(!gate.ShouldRun(true, false, 90, now.AddMinutes(12).AddSeconds(1)), "pressure must build again");
            Check(!gate.ShouldRun(true, false, 95, now.AddMinutes(30)), "sleep or polling gap resets sustained pressure");
            var configuredGate = new OptimizationGate();
            Check(!configuredGate.ShouldRun(true, false, 89, now, 90, 5), "custom threshold excludes lower usage");
            for (int second = 0; second < 30; second += 2) configuredGate.ShouldRun(true, false, 90, now.AddSeconds(second), 90, 5);
            Check(configuredGate.ShouldRun(true, false, 90, now.AddSeconds(30), 90, 5), "custom threshold triggers at equality");
            configuredGate.RecordAttempt(now.AddSeconds(30));
            Check(configuredGate.CooldownSeconds(now.AddSeconds(31), 5) == 299, "configured five-minute cooldown applies even after failure");
            configuredGate.ResetPressure();
            Check(configuredGate.CooldownSeconds(now.AddSeconds(31), 5) == 299, "changing options does not clear cooldown");
            var invalidGate = new OptimizationGate();
            invalidGate.ShouldRun(true, false, 95, now);
            invalidGate.ShouldRun(true, false, -1, now.AddSeconds(2));
            Check(invalidGate.PressureSecondsRemaining(now.AddSeconds(3)) == 30, "failed memory read resets pressure");

            // Reproduce the user's 60% threshold / 67% usage with the real gate
            // and optimizer, but a fake native backend so tests do not clean Windows.
            var reportedGate = new OptimizationGate();
            var reportedBackend = new FakeBackend();
            var reportedOptimizer = new MemoryOptimizer(reportedBackend);
            int automaticRuns = 0;
            for (int second = 0; second <= 90; second += 2)
            {
                var sampleTime = now.AddSeconds(second);
                if (reportedGate.ShouldRun(true, false, 67, sampleTime, 60, 10))
                {
                    Check(second == 30, "67% triggers at 30 seconds with a 60% threshold");
                    reportedGate.RecordAttempt(sampleTime);
                    var autoResult = reportedOptimizer.Optimize(OptimizationMode.Full);
                    Check(autoResult.Succeeded && autoResult.Completed.Count == 7, "automatic full mode reaches all seven backend operations");
                    automaticRuns++;
                }
                var status = reportedGate.GetStatus(true, false, false, 67, sampleTime, 60, 10);
                if (second == 10) Check(status.ShortText == "20秒后清理" && status.Details.Contains("60%"), "above-threshold waiting is visible on globe with seconds remaining");
                if (second == 40) Check(status.ShortText == "冷却 10分" && status.Details.Contains("9分50秒"), "above-threshold cooldown displays exact remaining time");
            }
            Check(automaticRuns == 1, "persistent 67% does not repeatedly clean during cooldown");
            Check(reportedGate.GetStatus(false, false, false, 67, now.AddSeconds(92), 60, 10).ShortText == "手动模式", "disabled mode overrides cooldown display");
            Check(reportedGate.GetStatus(true, false, true, 67, now.AddSeconds(92), 60, 10).ShortText == "清理中", "running cleanup overrides cooldown display");
            var idleGate = new OptimizationGate();
            Check(idleGate.GetStatus(true, false, false, 59, now, 60, 10).ShortText == "自动守护", "below threshold displays normal monitoring");
            Check(idleGate.GetStatus(true, false, false, -1, now, 60, 10).ShortText == "读取失败", "failed read is not displayed as threshold countdown");

            var fake = new FakeBackend();
            var optimizer = new MemoryOptimizer(fake);
            Check(optimizer.Optimize().AvailableChange == 128 * 1024 * 1024, "measures before/after delta");
            Check(fake.Purges == 1, "exactly one purge command");
            Check(OptimizationResult.Describe(-10).Contains("无明显变化"), "does not claim negative savings");
            fake.Fail = true;
            var failed = optimizer.Optimize();
            Check(!failed.Succeeded && failed.Completed.Count == 0 && failed.Describe().Contains("失败"), "native failure reported instead of false success");
            fake.Fail = false;
            Check(optimizer.Optimize().AvailableChange > 0, "failure releases busy lock");
            Check(fake.Trims == 0, "gentle mode never trims working sets");
            var full = optimizer.Optimize(OptimizationMode.WorkingSetsAndCache);
            Check(full.Succeeded && full.Completed.Count == 2 && fake.Trims == 1, "manual mode performs both real backend operations");
            Check(full.AvailableChange == 384L * 1024 * 1024 && full.Details().Contains("+384.0"), "report shows measured change from both operations");
            fake.TrimFail = true;
            var partial = optimizer.Optimize(OptimizationMode.WorkingSetsAndCache);
            Check(!partial.Succeeded && partial.Completed.Count == 1 && partial.Errors.Count == 1 && partial.Describe().Contains("部分完成"), "working-set failure still runs cache and reports partial result");
            fake.Fail = true;
            var bothFailed = optimizer.Optimize(OptimizationMode.WorkingSetsAndCache);
            Check(bothFailed.Completed.Count == 0 && bothFailed.Errors.Count == 2 && bothFailed.Describe().Contains("失败"), "both failures never claim cleanup success");
            fake.TrimFail = false;
            var cacheFailed = optimizer.Optimize(OptimizationMode.WorkingSetsAndCache);
            Check(cacheFailed.Completed.Count == 1 && cacheFailed.Errors.Count == 1, "successful working-set trim retained when cache fails");
            fake.Fail = false;
            fake.Block = true;
            var first = Task.Run(() => optimizer.Optimize());
            Check(fake.Entered.WaitOne(3000), "background cleaning started");
            bool rejected = false;
            try { optimizer.Optimize(); } catch (InvalidOperationException) { rejected = true; }
            fake.Release.Set(); first.Wait();
            Check(rejected, "concurrent cleaning rejected");

            var fullBackend = new FakeBackend();
            var fullOptimizer = new MemoryOptimizer(fullBackend);
            var progress = new System.Collections.Generic.List<string>();
            var complete = fullOptimizer.Optimize(OptimizationMode.Full, progress.Add);
            Check(complete.Succeeded && complete.Completed.Count == 7, "full mode executes seven cleaning areas");
            Check(string.Join(",", fullBackend.Steps) == "working,system,modified,standby,combine,registry,volume", "full cleanup follows upstream area order");
            Check(fullBackend.Purges == 0, "full standby purge replaces redundant low-priority purge");
            Check(progress.Count == 7 && progress[6].StartsWith("7/7"), "full cleanup reports step progress");
            fullBackend.FailStage = "system";
            fullBackend.Steps.Clear();
            var incomplete = fullOptimizer.Optimize(OptimizationMode.Full);
            Check(!incomplete.Succeeded && incomplete.Completed.Count == 6 && incomplete.Errors.Count == 1 && fullBackend.Steps.Count == 7, "failed area does not stop remaining full cleanup areas");
            fullBackend.FailStage = "all";
            var none = fullOptimizer.Optimize(OptimizationMode.Full);
            Check(none.Completed.Count == 0 && none.Errors.Count == 7 && none.Describe().Contains("失败"), "all unsupported or failed areas reported honestly");
            Check((OptimizationMode)typeof(OrbWindow).GetMethod("OptimizeAsync", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters()[0].DefaultValue == OptimizationMode.Full, "single click defaults to complete cleanup");

            string directory = Path.Combine(Path.GetTempPath(), "MemoryOrb-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings.xml");
            var defaults = OrbSettings.Load(path);
            Check(defaults.Diameter == 72 && defaults.AutoOptimize && defaults.AutoThreshold == 80 && defaults.AutoMode == OptimizationMode.WorkingSetsAndCache, "new installs default to balanced automatic guarding");
            File.WriteAllText(path, "<OrbSettings><AutoOptimize>false</AutoOptimize></OrbSettings>");
            var legacy = OrbSettings.Load(path);
            Check(!legacy.AutoOptimize && legacy.AutoThreshold == 80 && legacy.AutoCooldownMinutes == 10, "legacy disabled preference preserved with new defaults");
            var settings = new OrbSettings { Left = -900, Top = 250, Diameter = 88, AlwaysOnTop = false, AutoOptimize = true, AutoThreshold = 77, AutoCooldownMinutes = 15, AutoMode = OptimizationMode.Full };
            settings.Save(path);
            var saved = OrbSettings.Load(path);
            Check(saved.Left == -900 && saved.Top == 250 && saved.Diameter == 88 && saved.AutoOptimize && !saved.AlwaysOnTop, "settings round-trip including secondary screen");
            Check(saved.AutoThreshold == 77 && saved.AutoCooldownMinutes == 15 && saved.AutoMode == OptimizationMode.Full, "slider integer threshold and automatic options survive restart");
            settings.AutoThreshold = 5; settings.AutoCooldownMinutes = 0; settings.AutoMode = (OptimizationMode)99;
            settings.Diameter = 999; settings.Left = double.PositiveInfinity; settings.Save(path);
            saved = OrbSettings.Load(path);
            Check(saved.Diameter == 72 && double.IsNaN(saved.Left), "invalid size and coordinates normalized");
            Check(saved.AutoThreshold == 80 && saved.AutoCooldownMinutes == 10 && saved.AutoMode == OptimizationMode.WorkingSetsAndCache, "invalid auto values normalized");
            File.WriteAllText(path, "broken xml");
            Check(OrbSettings.Load(path).Diameter == 72, "corrupt settings recovery");
            File.Delete(path); Directory.Delete(directory);

            var memory = new WindowsMemoryBackend().Read();
            Check(memory.Total > 0 && memory.Load >= 0 && memory.Load <= 100 && memory.Available <= memory.Total, "real Windows memory query");
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            // A plain test host cannot acquire the production mutex or start a
            // real cleaner when PushFrame dispatches the queued Startup event.
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var window = new OrbWindow(true);
            window.Show();
            window.UpdateLayout();
            Check(window.AllowsTransparency && window.WindowStyle == WindowStyle.None && !window.ShowInTaskbar, "transparent borderless orb window");
            var button = (Button)window.FindName("OrbButton");
            Check(button.ContextMenu.Items.Count >= 10, "context menu and exit available");
            MenuItem autoToggle = null, autoOptions = null;
            foreach (object entry in button.ContextMenu.Items)
            {
                var item = entry as MenuItem;
                if (item == null) continue;
                if ((item.Header as string ?? "").StartsWith("启用自动清理")) autoToggle = item;
                if ((item.Header as string) == "自动清理设置") autoOptions = item;
            }
            Check(autoToggle != null && autoToggle.IsCheckable && autoToggle.IsChecked, "automatic cleaning is an enabled checkbox on fresh install");
            var visibleAutoStatus = (MenuItem)typeof(OrbWindow).GetField("autoStatusItem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            Check(button.ContextMenu.Items.Contains(visibleAutoStatus), "automatic status is visible in main context menu without opening a submenu");
            Check(((TextBlock)window.FindName("StateText")).Text == "演示" && (button.ToolTip as string).Contains("演示模式不执行自动清理"), "globe and hover tooltip reflect actual automatic status");
            var sliderHost = (MenuItem)autoOptions.Items[0];
            var sliderPanel = (StackPanel)sliderHost.Header;
            var slider = (Slider)sliderPanel.Children[1];
            Check(sliderHost.StaysOpenOnClick && slider.Minimum == 60 && slider.Maximum == 95 && slider.IsSnapToTickEnabled, "threshold slider stays open and allows integer values 60 to 95");
            slider.Value = 77;
            var liveSettings = (OrbSettings)typeof(OrbWindow).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            Check(liveSettings.AutoThreshold == 77 && ((TextBlock)sliderPanel.Children[0]).Text.Contains("77%"), "moving slider updates configured threshold and visible value");
            autoToggle.IsChecked = false;
            autoToggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(!liveSettings.AutoOptimize, "unchecking automatic cleaning disables it");
            button.ApplyTemplate();
            Check(button.Template.FindName("Earth", button) is EarthView, "real 3D globe is present in button template");
            foreach (var point in new[] { new Point(48, 48), new Point(24, 32), new Point(72, 48), new Point(48, 75) })
            {
                var hit = window.InputHitTest(button.TranslatePoint(point, window)) as DependencyObject;
                while (hit != null && hit != button) hit = VisualTreeHelper.GetParent(hit);
                Check(hit == button, "visible globe point receives pointer input: " + point);
            }
            var request = Branding.CreateHomepageRequest();
            Check(request.UseShellExecute && request.FileName == "https://blog.csdn.net/syq10086?type=blog", "author homepage opens exact URL in default browser");
            Check(window.Title.StartsWith("内存卫士"), "new product name is displayed");
            Check(Branding.Version == "0.6.3", "about version comes from built assembly");
            var downloadRequest = Branding.CreateDownloadRequest();
            Check(downloadRequest.UseShellExecute && downloadRequest.FileName == "https://www.xiaopuwa.com/memory-guardian/", "update link opens product download page in default browser");
            bool hasDownload = false;
            foreach (object entry in button.ContextMenu.Items)
                if (entry is MenuItem item && (item.Header as string) == "更新与下载 ↗" && (item.ToolTip as string) == Branding.DownloadPage) hasDownload = true;
            Check(hasDownload, "main context menu includes update download entry");
            var aboutWindow = (Window)typeof(OrbWindow).GetMethod("CreateAboutWindow", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            var aboutPanel = (StackPanel)((ScrollViewer)aboutWindow.Content).Content;
            bool aboutDownload = false, aboutVersion = false;
            foreach (UIElement entry in aboutPanel.Children)
            {
                if (entry is Button aboutButton && (aboutButton.ToolTip as string) == Branding.DownloadPage) aboutDownload = true;
                if (entry is TextBlock aboutText && aboutText.Text.StartsWith(Branding.Version + " ·")) aboutVersion = true;
            }
            Check(aboutWindow.Title == "关于内存卫士" && aboutDownload && aboutVersion, "about dialog includes renamed title, actual version and update button");
            aboutWindow.Close();
            var supportWindow = (Window)typeof(OrbWindow).GetMethod("CreateSupportWindow", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            var supportPanel = (StackPanel)((ScrollViewer)supportWindow.Content).Content;
            var supportImage = (Image)supportPanel.Children[2];
            Check(supportImage.Source.Width > 1000 && supportImage.Source.Height > 1500, "support dialog loads full original payment image from embedded resources");
            supportWindow.Close();
            var earthView = (EarthView)button.Template.FindName("Earth", button);
            Check(MemoryPressurePalette.Band(0) == 0 && MemoryPressurePalette.Band(50) == 0 && MemoryPressurePalette.Band(95) == 20 && MemoryPressurePalette.Band(100) == 20, "pressure palette endpoints remain blue below 50 and red from 95 percent");
            int previousBand = -1;
            bool monotonic = true;
            for (int load = 0; load <= 100; load++) { int band = MemoryPressurePalette.Band(load); monotonic &= band >= previousBand; previousBand = band; }
            Check(monotonic, "increasing memory load never moves toward a lower pressure band");
            var blue = MemoryPressurePalette.Surface(false, 0);
            var red = MemoryPressurePalette.Surface(false, 20);
            Check(blue.B > blue.R && red.R > red.B, "ocean colors move from blue to red while keeping land contrast");
            earthView.SetMemoryLoad(25);
            Check(earthView.PressureBand == 0, "real 3D earth accepts low memory load");
            earthView.SetMemoryLoad(95);
            Check(earthView.PressureBand == 20, "real 3D earth accepts high memory load without frozen material error");
            earthView.SetMemoryLoad(-1);
            Check(earthView.PressureBand == 20, "invalid reading retains last known globe color");
            earthView.SetMemoryLoad(25);
            Check(earthView.PressureBand == 0, "globe returns to blue after memory drops using cached texture");
            var optimize = typeof(OrbWindow).GetMethod("OptimizeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Console.WriteLine("Checking asynchronous demo cleanup...");
            var cleaning = (Task)optimize.Invoke(window, new object[] { OptimizationMode.Full, false });
            Console.WriteLine("Demo started; pumping UI dispatcher...");
            var frame = new DispatcherFrame();
            var deadline = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            deadline.Tick += (s, e) => { deadline.Stop(); frame.Continue = false; };
            deadline.Start();
            cleaning.ContinueWith(t => window.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
            Dispatcher.PushFrame(frame);
            deadline.Stop();
            if (!cleaning.IsCompleted) throw new TimeoutException("Demo did not finish within 15 seconds");
            cleaning.GetAwaiter().GetResult();
            Check(((TextBlock)window.FindName("BadgeText")).Text.Contains("未执行清理"), "demo explicitly labels simulated completion");
            Check(((FrameworkElement)window.FindName("RingBox")).Visibility == Visibility.Collapsed, "animation stops after cleaning");
            var badgeTimer = typeof(OrbWindow).GetField("badgeTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            ((DispatcherTimer)badgeTimer.GetValue(window)).Stop();
            ((FrameworkElement)window.FindName("Badge")).Visibility = Visibility.Collapsed;
            ((TextBlock)window.FindName("StateText")).Text = "内存";
            if (args.Length > 0)
            {
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)(window.Width * 3), (int)(window.Height * 3), 288, 288, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(args[0])) png.Save(file);
                foreach (int sample in new[] { 25, 75, 95 })
                {
                    earthView.SetMemoryLoad(sample);
                    ((TextBlock)window.FindName("PercentText")).Text = sample + "%";
                    window.UpdateLayout();
                    var swatch = new RenderTargetBitmap((int)(window.Width * 3), (int)(window.Height * 3), 288, 288, PixelFormats.Pbgra32);
                    swatch.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(swatch));
                    string samplePath = Path.Combine(Path.GetDirectoryName(args[0]), "memory-pressure-" + sample + ".png");
                    using (var file = File.Create(samplePath)) encoder.Save(file);
                }
            }
            using (var stream = typeof(OrbWindow).Assembly.GetManifestResourceStream("MemoryOrb.LICENSE"))
                Check(stream != null && stream.Length > 30000, "GPL license embedded in EXE");
            window.Close();
            Console.WriteLine("All " + count + " checks passed. Real elevated cleanup and startup task changes are not exercised by this suite.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private class FakeBackend : IMemoryBackend
    {
        public int Purges, Trims;
        public bool Fail, Block, TrimFail;
        public string FailStage;
        public System.Collections.Generic.List<string> Steps = new System.Collections.Generic.List<string>();
        public ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
        private long available = 1024L * 1024 * 1024;
        public MemorySnapshot Read() => new MemorySnapshot { Available = available, Total = 8L * 1024 * 1024 * 1024, Load = 80 };
        public void PurgeLowPriorityStandby()
        {
            if (Fail) throw new InvalidOperationException("test failure");
            if (Block) { Entered.Set(); Release.WaitOne(); }
            Purges++; available += 128 * 1024 * 1024;
        }
        public void TrimWorkingSets()
        {
            Step("working");
            if (TrimFail) throw new InvalidOperationException("working-set test failure");
            Trims++; available += 256 * 1024 * 1024;
        }
        private void Step(string name)
        {
            Steps.Add(name);
            if (FailStage == name || FailStage == "all") throw new InvalidOperationException("unsupported or failed: " + name);
        }
        public void TrimSystemFileCache() { Step("system"); }
        public void FlushModifiedPages() { Step("modified"); }
        public void PurgeAllStandby() { Step("standby"); }
        public void CombinePages() { Step("combine"); }
        public void ReconcileRegistry() { Step("registry"); }
        public void FlushVolumeCaches() { Step("volume"); }
    }
}
