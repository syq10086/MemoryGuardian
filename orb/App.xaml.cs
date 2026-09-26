// MemoryOrb modifications, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows;

namespace MemoryOrb
{
    public partial class App : Application
    {
        private Mutex instance;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool demo = e.Args.Contains("--demo");
            bool acquired;
            instance = new Mutex(true, @"Local\MemoryOrb-" + WindowsIdentity.GetCurrent().User.Value + (demo ? "-demo" : ""), out acquired);
            if (!acquired)
            {
                MessageBox.Show("内存卫士已经在运行。可从系统托盘右键菜单找回悬浮球。", "内存卫士");
                Shutdown();
                return;
            }
            DispatcherUnhandledException += (sender, args) =>
            {
                WriteError(args.Exception);
                MessageBox.Show("内存卫士遇到错误，请重新打开。\n" + args.Exception.Message, "内存卫士");
                args.Handled = true;
                Shutdown(1);
            };
            MainWindow = new OrbWindow(demo);
            // Expose only the demo in the taskbar for UI inspection and easy recovery.
            if (demo) MainWindow.ShowInTaskbar = true;
            MainWindow.Show();
        }

        internal static void WriteError(Exception error)
        {
            try
            {
                Directory.CreateDirectory(OrbSettings.DirectoryPath);
                File.WriteAllText(Path.Combine(OrbSettings.DirectoryPath, "last-error.txt"), DateTime.Now + "\n" + error);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (instance != null) instance.Dispose();
            base.OnExit(e);
        }
    }
}
