// MemoryOrb modifications, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace MemoryOrb
{
    public partial class OrbWindow : Window
    {
        private readonly bool demo;
        private readonly OrbSettings settings;
        private readonly WindowsMemoryBackend backend = new WindowsMemoryBackend();
        private readonly MemoryOptimizer optimizer;
        private readonly OptimizationGate gate = new OptimizationGate();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private readonly DispatcherTimer badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        private readonly ContextMenu menu = new ContextMenu();
        private readonly MenuItem cleanItem = new MenuItem { Header = "一键完整清理（7 项）", ToolTip = "工作集、系统文件缓存、已修改页面、全部待机缓存、合并页面、注册表缓存、磁盘写入缓存。执行时可能短暂卡顿。" };
        private readonly MenuItem autoItem = new MenuItem { Header = "启用自动清理", IsCheckable = true };
        private readonly MenuItem autoStatusItem = new MenuItem { Header = "自动清理", IsEnabled = false };
        private readonly MenuItem topItem = new MenuItem { Header = "始终置顶", IsCheckable = true };
        private readonly MenuItem startupItem = new MenuItem { Header = "登录时启动", IsCheckable = true, IsEnabled = false };
        private readonly MenuItem statsItem = new MenuItem { Header = "正在读取内存…", IsEnabled = false };
        private Forms.NotifyIcon tray;
        private System.Drawing.Icon trayIcon;
        private bool busy, closing, pointerDown, dragged;
        private Point pressScreen;
        private double pressLeft, pressTop;
        private DateTime nextManual = DateTime.MinValue;

        public OrbWindow(bool demo = false)
        {
            InitializeComponent();
            this.demo = demo;
            optimizer = new MemoryOptimizer(backend);
            settings = demo ? new OrbSettings() : OrbSettings.Load(OrbSettings.FilePath);
            Topmost = settings.AlwaysOnTop;
            if (demo) Title += " · 演示";
            ApplySize();
            BuildMenu();
            Loaded += OnLoaded;
            Closed += OnClosed;
            timer.Tick += OnTick;
            badgeTimer.Tick += (s, e) => { Badge.Visibility = Visibility.Collapsed; badgeTimer.Stop(); };
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        private void BuildMenu()
        {
            cleanItem.Click += async (s, e) => await OptimizeAsync();
            menu.Items.Add(new MenuItem { Header = demo ? "内存卫士 · 演示" : "内存卫士", IsEnabled = false });
            menu.Items.Add(statsItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(cleanItem);
            var gentle = new MenuItem { Header = "仅清理低优先级缓存" };
            gentle.Click += async (s, e) => await OptimizeAsync(OptimizationMode.Gentle);
            menu.Items.Add(gentle);
            var resultItem = new MenuItem { Header = "查看上次优化结果" };
            resultItem.Click += (s, e) => MessageBox.Show(this, lastResult ?? "本次运行尚未执行优化。", "内存卫士 · 优化结果");
            menu.Items.Add(resultItem);
            autoItem.IsChecked = settings.AutoOptimize;
            autoItem.Click += (s, e) => { settings.AutoOptimize = autoItem.IsChecked; AutoSettingsChanged(); ShowBadge(settings.AutoOptimize ? "自动清理已开启" : "自动清理已关闭"); };
            menu.Items.Add(autoItem);
            menu.Items.Add(autoStatusItem);
            BuildAutoMenu();
            topItem.IsChecked = settings.AlwaysOnTop;
            topItem.Click += (s, e) => { Topmost = settings.AlwaysOnTop = topItem.IsChecked; SaveSettings(); };
            menu.Items.Add(topItem);
            var sizes = new MenuItem { Header = "球体大小" };
            foreach (int size in new[] { 56, 72, 88 })
            {
                var item = new MenuItem { Header = size == 56 ? "小" : size == 72 ? "标准" : "大", IsCheckable = true, IsChecked = settings.Diameter == size, Tag = size };
                item.Click += (s, e) =>
                {
                    settings.Diameter = (int)item.Tag;
                    foreach (MenuItem sibling in sizes.Items) sibling.IsChecked = sibling == item;
                    ApplySize(); ClampToScreen(); SaveSettings();
                };
                sizes.Items.Add(item);
            }
            menu.Items.Add(sizes);
            startupItem.Click += async (s, e) =>
            {
                bool desired = startupItem.IsChecked;
                startupItem.IsEnabled = false;
                try { await Task.Run(() => StartupTask.SetEnabled(desired)); ShowBadge(desired ? "已开启登录启动" : "已关闭登录启动"); }
                catch (Exception ex) { startupItem.IsChecked = !desired; App.WriteError(ex); ShowBadge("设置失败 · 请重试"); }
                finally { startupItem.IsEnabled = !demo; }
            };
            menu.Items.Add(startupItem);
            var errorItem = new MenuItem { Header = "查看上次错误" };
            errorItem.Click += (s, e) => MessageBox.Show(this, lastError ?? "没有记录到优化错误。", "内存卫士");
            menu.Items.Add(errorItem);
            menu.Items.Add(new Separator());
            var center = new MenuItem { Header = "找回悬浮球" };
            center.Click += (s, e) => RestoreOrb();
            menu.Items.Add(center);
            var homepage = new MenuItem { Header = "作者主页 ↗", ToolTip = Branding.AuthorHomepage };
            homepage.Click += (s, e) => OpenAuthorHomepage();
            menu.Items.Add(homepage);
            var github = new MenuItem { Header = "GitHub 项目源码 ↗", ToolTip = Branding.GitHubRepository };
            github.Click += (s, e) => OpenGitHub();
            menu.Items.Add(github);
            var support = new MenuItem { Header = "打赏作者 · 自愿支持" };
            support.Click += (s, e) => ShowSupport();
            menu.Items.Add(support);
            var download = new MenuItem { Header = "更新与下载 ↗", ToolTip = Branding.DownloadPage };
            download.Click += (s, e) => OpenDownloadPage();
            menu.Items.Add(download);
            var about = new MenuItem { Header = "关于与开源许可" };
            about.Click += (s, e) => ShowAbout();
            menu.Items.Add(about);
            menu.Items.Add(new Separator());
            var exit = new MenuItem { Header = "退出" };
            exit.Click += (s, e) => Close();
            menu.Items.Add(exit);
            OrbButton.ContextMenu = menu;
        }

        private void BuildAutoMenu()
        {
            var options = new MenuItem { Header = "自动清理设置" };
            var thresholdPanel = new StackPanel { Width = 238, Margin = new Thickness(4, 6, 4, 6) };
            var thresholdLabel = new TextBlock { Text = "内存占用达到 " + settings.AutoThreshold + "% 时清理", FontWeight = FontWeights.SemiBold };
            var thresholdSlider = new Slider
            {
                Name = "AutoThresholdSlider", Minimum = 60, Maximum = 95, Value = settings.AutoThreshold,
                TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 5,
                IsMoveToPointEnabled = true, AutoToolTipPlacement = AutoToolTipPlacement.TopLeft,
                AutoToolTipPrecision = 0, Margin = new Thickness(0, 12, 0, 8)
            };
            System.Windows.Automation.AutomationProperties.SetName(thresholdSlider, "自动清理阈值百分比");
            thresholdSlider.ValueChanged += (s, e) =>
            {
                settings.AutoThreshold = (int)Math.Round(e.NewValue);
                thresholdLabel.Text = "内存占用达到 " + settings.AutoThreshold + "% 时清理";
                AutoSettingsChanged();
            };
            thresholdPanel.Children.Add(thresholdLabel);
            thresholdPanel.Children.Add(thresholdSlider);
            thresholdPanel.Children.Add(new TextBlock { Text = "60%                         推荐 80%          95%", FontSize = 11, Foreground = Brushes.DimGray });
            thresholdPanel.Children.Add(new TextBlock { Text = "拖动滑块调整 · 持续 30 秒后触发", FontSize = 11, Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.DimGray });
            options.Items.Add(new MenuItem { Header = thresholdPanel, StaysOpenOnClick = true });
            options.Items.Add(new Separator());
            var cooldown = new MenuItem { Header = "两次清理最短间隔" };
            foreach (int value in new[] { 5, 10, 15, 30 })
            {
                var item = new MenuItem { Header = value + " 分钟", IsCheckable = true, IsChecked = settings.AutoCooldownMinutes == value };
                item.Click += (s, e) => { settings.AutoCooldownMinutes = value; CheckOnly(cooldown, item); AutoSettingsChanged(); };
                cooldown.Items.Add(item);
            }
            options.Items.Add(cooldown);
            var strength = new MenuItem { Header = "自动清理强度" };
            foreach (OptimizationMode value in new[] { OptimizationMode.WorkingSetsAndCache, OptimizationMode.Gentle, OptimizationMode.Full })
            {
                var item = new MenuItem { Header = AutoModeName(value), IsCheckable = true, IsChecked = settings.AutoMode == value,
                    ToolTip = value == OptimizationMode.Full ? "完整七项清理可能短暂卡顿并刷新磁盘缓存，请按需要选用。" : "仅在持续高占用时触发，不会按固定时间反复清理。" };
                item.Click += (s, e) => { settings.AutoMode = value; CheckOnly(strength, item); AutoSettingsChanged(); };
                strength.Items.Add(item);
            }
            options.Items.Add(strength);
            menu.Items.Add(options);
            RefreshAutoLabel();
        }

        private static void CheckOnly(MenuItem parent, MenuItem selected)
        {
            foreach (MenuItem item in parent.Items) item.IsChecked = item == selected;
        }
        private static string AutoModeName(OptimizationMode mode)
        {
            return mode == OptimizationMode.Gentle ? "温和：仅低优先级缓存" : mode == OptimizationMode.Full ? "完整：全部七项" : "均衡：工作集和缓存（推荐）";
        }
        private void RefreshAutoLabel()
        {
            autoItem.Header = "启用自动清理（≥ " + settings.AutoThreshold + "%）";
            autoItem.ToolTip = "持续 30 秒后执行；最短间隔 " + settings.AutoCooldownMinutes + " 分钟；" + AutoModeName(settings.AutoMode);
        }
        private void AutoSettingsChanged()
        {
            gate.ResetPressure(); RefreshAutoLabel(); SaveSettings();
            UpdateMemory();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top)) { Left = settings.Left; Top = settings.Top; }
            else PositionDefault();
            ClampToScreen();
            CreateTray();
            UpdateMemory();
            timer.Start();
            if (demo) ShowBadge("演示模式 · 不执行清理");
            else
            {
                try { startupItem.IsChecked = await Task.Run(() => StartupTask.IsEnabled()); startupItem.IsEnabled = true; }
                catch (Exception ex) { App.WriteError(ex); }
            }
        }

        private void CreateTray()
        {
            using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/EarthGuardian;component/Assets/EarthGuardian.ico")).Stream)
                trayIcon = new System.Drawing.Icon(stream);
            tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "内存卫士", Visible = true };
            tray.MouseClick += (s, e) => Dispatcher.Invoke(new Action(() =>
            {
                if (e.Button == Forms.MouseButtons.Right)
                {
                    Activate(); menu.PlacementTarget = OrbButton; menu.Placement = PlacementMode.MousePoint; menu.IsOpen = true;
                }
                else if (e.Button == Forms.MouseButtons.Left) RestoreOrb();
            }));
        }

        private async void OnTick(object sender, EventArgs e)
        {
            if (closing) return;
            int load = UpdateMemory();
            DateTime now = DateTime.UtcNow;
            bool run = gate.ShouldRun(settings.AutoOptimize && !demo, busy, load, now, settings.AutoThreshold, settings.AutoCooldownMinutes);
            RefreshAutoStatus(load, now);
            if (run) await OptimizeAsync(settings.AutoMode, true);
        }

        private void RefreshAutoStatus(int load, DateTime now)
        {
            var status = gate.GetStatus(settings.AutoOptimize, demo, busy, load, now, settings.AutoThreshold, settings.AutoCooldownMinutes);
            autoStatusItem.Header = status.Details;
            if (!busy) StateText.Text = status.ShortText;
            OrbButton.ToolTip = statsItem.Header + "\n" + status.Details + "\n自动强度：" + AutoModeName(settings.AutoMode)
                + "\n手动或自动清理后，自动模式间隔 " + settings.AutoCooldownMinutes + " 分钟；超标持续 30 秒后触发。"
                + "\n单击立即完整清理 7 项 · 拖动移动 · 右键查看结果和设置";
        }

        private int UpdateMemory()
        {
            try
            {
                var value = backend.Read();
                OrbButton.ApplyTemplate();
                (OrbButton.Template.FindName("Earth", OrbButton) as EarthView)?.SetMemoryLoad(value.Load);
                if (!busy)
                {
                    PercentText.Text = value.Load + "%";
                    PercentText.Foreground = new SolidColorBrush(value.Load >= 85 ? Color.FromRgb(255, 218, 147) : Colors.White);
                }
                string stats = "内存 " + value.Load + "% · 可用 " + (value.Available / 1073741824.0).ToString("0.0") + " / " + (value.Total / 1073741824.0).ToString("0.0") + " GB";
                statsItem.Header = stats;
                RefreshAutoStatus(value.Load, DateTime.UtcNow);
                if (tray != null) tray.Text = "内存卫士 · " + value.Load + "%";
                return value.Load;
            }
            catch (Exception ex)
            {
                if (!busy) { PercentText.Text = "—"; StateText.Text = "读取失败"; }
                RefreshAutoStatus(-1, DateTime.UtcNow);
                OrbButton.ToolTip += "\n" + ex.Message;
                return -1;
            }
        }

        internal async Task OptimizeAsync(OptimizationMode mode = OptimizationMode.Full, bool automatic = false)
        {
            if (closing) return;
            if (busy) { ShowBadge("正在优化，请稍候"); return; }
            if (DateTime.UtcNow < nextManual) { ShowBadge("请稍候 " + Math.Ceiling((nextManual - DateTime.UtcNow).TotalSeconds) + " 秒再试"); return; }
            busy = true;
            gate.RecordAttempt(DateTime.UtcNow);
            RefreshAutoStatus(-1, DateTime.UtcNow);
            cleanItem.IsEnabled = false;
            badgeTimer.Stop(); Badge.Visibility = Visibility.Collapsed;
            PercentText.Text = "···"; StateText.Text = "优化中";
            RingBox.Visibility = Visibility.Visible;
            var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(animation, 30);
            Spin.BeginAnimation(RotateTransform.AngleProperty, animation);
            SetEarthOptimizing(true);
            try
            {
                if (demo) { await Task.Delay(1200); if (!closing) ShowBadge("演示完成 · 未执行清理"); }
                else
                {
                    var minimumFeedback = Task.Delay(700);
                    var result = await Task.Run(() => optimizer.Optimize(mode, step =>
                    {
                        if (!Dispatcher.HasShutdownStarted)
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (!closing && busy) ShowBadge("正在清理 " + step);
                            }));
                    }));
                    result.Trigger = automatic ? "自动：占用 ≥ " + settings.AutoThreshold + "% 持续 30 秒" : "手动点击";
                    lastResult = result.Details();
                    lastError = result.Succeeded ? null : string.Join("\n", result.Errors);
                    try { result.SaveReport(); } catch (Exception reportError) { App.WriteError(reportError); }
                    await minimumFeedback;
                    if (!closing) ShowBadge(result.Describe(), !result.Succeeded);
                }
            }
            catch (Exception ex)
            {
                App.WriteError(ex);
                if (!closing) { ShowBadge("优化未完成 · 右键查看原因", true); statsItem.Header = "上次优化失败"; }
                lastError = ex.Message;
                lastResult = "优化未完成：" + ex.Message;
            }
            finally
            {
                Spin.BeginAnimation(RotateTransform.AngleProperty, null);
                SetEarthOptimizing(false);
                RingBox.Visibility = Visibility.Collapsed;
                busy = false; cleanItem.IsEnabled = true;
                nextManual = DateTime.UtcNow.AddSeconds(5);
                if (!closing) UpdateMemory();
            }
        }

        private string lastError;
        private string lastResult;
        private void SetEarthOptimizing(bool active)
        {
            OrbButton.ApplyTemplate();
            var earth = OrbButton.Template.FindName("Earth", OrbButton) as EarthView;
            if (earth != null) earth.SetOptimizing(active);
        }

        private void OpenAuthorHomepage()
        {
            try { Process.Start(Branding.CreateHomepageRequest()); }
            catch (Exception ex) { App.WriteError(ex); ShowBadge("无法打开浏览器", true); }
        }
        private void OpenGitHub()
        {
            try { Process.Start(Branding.CreateGitHubRequest()); }
            catch (Exception ex) { App.WriteError(ex); ShowBadge("无法打开 GitHub，请稍后重试", true); }
        }
        private void OpenDownloadPage()
        {
            try { Process.Start(Branding.CreateDownloadRequest()); }
            catch (Exception ex) { App.WriteError(ex); ShowBadge("无法打开下载页，请稍后重试", true); }
        }
        private void ShowBadge(string text, bool error = false)
        {
            BadgeText.Text = text;
            BadgeText.Foreground = new SolidColorBrush(error ? Color.FromRgb(255, 213, 166) : Color.FromRgb(221, 248, 255));
            Badge.Visibility = Visibility.Visible;
            badgeTimer.Stop(); badgeTimer.Start();
        }

        private void ApplySize()
        {
            OrbBox.Width = OrbBox.Height = RingBox.Width = RingBox.Height = settings.Diameter * 96.0 / 82.0;
            Height = OrbBox.Height + 38;
        }

        private void OnPointerDown(object sender, MouseButtonEventArgs e)
        {
            pointerDown = true; dragged = false;
            pressScreen = PointToScreen(e.GetPosition(this));
            pressLeft = Left; pressTop = Top;
            OrbButton.CaptureMouse(); OrbButton.Focus(); e.Handled = true;
        }

        private void OnPointerMove(object sender, MouseEventArgs e)
        {
            if (!pointerDown || e.LeftButton != MouseButtonState.Pressed) return;
            var now = PointToScreen(e.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            Vector delta = source.CompositionTarget.TransformFromDevice.Transform(now - pressScreen);
            if (!dragged && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragged = true;
            Left = pressLeft + delta.X; Top = pressTop + delta.Y;
            e.Handled = true;
        }

        private async void OnPointerUp(object sender, MouseButtonEventArgs e)
        {
            if (!pointerDown) return;
            bool click = !dragged;
            pointerDown = false; OrbButton.ReleaseMouseCapture();
            if (!click) { ClampToScreen(); SaveSettings(); }
            e.Handled = true;
            if (click) await OptimizeAsync();
        }

        private void OnLostCapture(object sender, MouseEventArgs e) { pointerDown = false; }
        private async void OnKeyboardClick(object sender, RoutedEventArgs e) { await OptimizeAsync(); }

        private void PositionDefault()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 18; Top = area.Top + area.Height * 0.65;
        }

        internal void ClampToScreen()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            var area = Forms.Screen.FromHandle(handle).WorkingArea;
            var transform = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new Point(area.Left, area.Top));
            var bottomRight = transform.Transform(new Point(area.Right, area.Bottom));
            Left = Math.Max(topLeft.X, Math.Min(Left, bottomRight.X - Width));
            Top = Math.Max(topLeft.Y, Math.Min(Top, bottomRight.Y - Height));
        }

        private void OnDisplayChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(() => { if (!closing) ClampToScreen(); }));
        }

        private void RestoreOrb() { PositionDefault(); ClampToScreen(); Show(); Activate(); SaveSettings(); }

        private void SaveSettings()
        {
            if (demo) return;
            try { settings.Left = Left; settings.Top = Top; settings.Save(OrbSettings.FilePath); }
            catch (Exception ex) { App.WriteError(ex); if (!closing) ShowBadge("位置或设置未能保存", true); }
        }

        private void ShowSupport()
        {
            CreateSupportWindow().ShowDialog();
        }

        private Window CreateSupportWindow()
        {
            var dialog = new Window
            {
                Title = "打赏作者 · 内存卫士", Owner = this,
                Width = 480, Height = 740,
                MaxHeight = SystemParameters.WorkArea.Height,
                MaxWidth = SystemParameters.WorkArea.Width,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize, Background = Brushes.White
            };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "感谢支持宋域强", FontSize = 22, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = "自愿支持，感谢鼓励。软件永久免费，\n不打赏也可使用全部功能。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) });
            var image = new Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/EarthGuardian;component/Assets/wechat-support.jpg")),
                Stretch = Stretch.Uniform, MaxWidth = 400,
                ToolTip = "使用微信扫一扫，自愿选择金额"
            };
            panel.Children.Add(image);
            panel.Children.Add(new TextBlock { Text = "使用微信扫一扫，请核对收款人后自愿选择金额。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12), Foreground = Brushes.DimGray });
            var close = new Button { Content = "关闭", Padding = new Thickness(16, 6, 16, 6), IsCancel = true };
            close.Click += (s, e) => dialog.Close();
            panel.Children.Add(close);
            dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            return dialog;
        }

        private void ShowAbout()
        {
            CreateAboutWindow().ShowDialog();
        }

        private Window CreateAboutWindow()
        {
            var dialog = new Window { Title = "关于内存卫士", Width = 460, Height = 490, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily, Background = new SolidColorBrush(Color.FromRgb(242, 247, 252)) };
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "内存卫士", FontSize = 24, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = Branding.Version + " · 单击清理，守护内存", Margin = new Thickness(0, 6, 0, 8) });
            panel.Children.Add(new TextBlock { Text = "开发者：宋域强 · 公众号 / 抖音：宋域强", Margin = new Thickness(0, 0, 0, 14) });
            var authorButton = new Button { Content = "作者主页 · 宋域强 ↗", ToolTip = Branding.AuthorHomepage, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8) };
            authorButton.Click += (s, e) => OpenAuthorHomepage();
            panel.Children.Add(authorButton);
            var githubButton = new Button { Content = "GitHub · syq10086 / MemoryGuardian ↗", ToolTip = Branding.GitHubRepository, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8) };
            githubButton.Click += (s, e) => OpenGitHub();
            panel.Children.Add(githubButton);
            var supportButton = new Button { Content = "打赏作者 · 自愿支持", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8) };
            supportButton.Click += (s, e) => ShowSupport();
            panel.Children.Add(supportButton);
            var downloadButton = new Button { Content = "更新与下载 · 内存卫士 ↗", ToolTip = Branding.DownloadPage, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6) };
            downloadButton.Click += (s, e) => OpenDownloadPage();
            panel.Children.Add(downloadButton);
            panel.Children.Add(new TextBlock { Text = Branding.DownloadPage, FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 14) });
            panel.Children.Add(new TextBlock { Text = "基于 Windows Memory Cleaner 的衍生作品。\n原项目：Igor Mundstein 与贡献者。\n\n按 GNU GPL v3 分发，允许按许可证复制、修改与再分发；不提供担保。修改日期：2026-09-26。\n\n手动执行七项完整清理；自动模式可设置阈值、间隔和强度。数字是系统实测变化，可能回升，不代表提速。", TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(lastError)) panel.Children.Add(new TextBlock { Text = "上次错误：" + lastError, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Foreground = Brushes.DarkRed });
            var licenseButton = new Button { Content = "查看 GPL v3 许可证", Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(10, 6, 10, 6) };
            licenseButton.Click += (s, e) =>
            {
                string text;
                using (var reader = new StreamReader(typeof(OrbWindow).Assembly.GetManifestResourceStream("MemoryOrb.LICENSE"))) text = reader.ReadToEnd();
                var license = new Window { Title = "GNU GPL v3", Owner = dialog, Width = 720, Height = 540, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                license.Content = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16) };
                license.ShowDialog();
            };
            panel.Children.Add(licenseButton);
            dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            return dialog;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            closing = true; timer.Stop(); badgeTimer.Stop(); SetEarthOptimizing(false);
            SaveSettings();
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (trayIcon != null) trayIcon.Dispose();
        }
    }
}
