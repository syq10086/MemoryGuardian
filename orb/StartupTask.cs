// MemoryOrb modifications, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace MemoryOrb
{
    internal static class StartupTask
    {
        private static string TaskName => "MemoryOrb-" + WindowsIdentity.GetCurrent().User.Value;
        internal static bool IsEnabled() { return Run("/Query /TN \"" + TaskName + "\"", false); }

        internal static void SetEnabled(bool enabled)
        {
            if (!enabled) { Run("/Delete /F /TN \"" + TaskName + "\"", true); return; }
            string sid = SecurityElement.Escape(WindowsIdentity.GetCurrent().User.Value);
            string executable = SecurityElement.Escape(Process.GetCurrentProcess().MainModule.FileName);
            string xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>内存卫士：当前用户登录时启动</Description></RegistrationInfo>" +
                "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + sid + "</UserId></LogonTrigger></Triggers>" +
                "<Principals><Principal id=\"User\"><UserId>" + sid + "</UserId><LogonType>InteractiveToken</LogonType>" +
                "<RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings>" +
                "<Actions Context=\"User\"><Exec><Command>" + executable + "</Command></Exec></Actions></Task>";
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, xml, Encoding.Unicode);
                Run("/Create /F /TN \"" + TaskName + "\" /XML \"" + path + "\"", true);
            }
            finally { File.Delete(path); }
        }

        private static bool Run(string arguments, bool throwOnFailure)
        {
            using (var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true
            }))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(15000)) { process.Kill(); throw new TimeoutException("登录启动设置超时。"); }
                System.Threading.Tasks.Task.WaitAll(stdout, stderr);
                if (process.ExitCode != 0 && throwOnFailure)
                    throw new InvalidOperationException("无法更新登录启动设置：" + stderr.Result.Trim());
                return process.ExitCode == 0;
            }
        }
    }
}
