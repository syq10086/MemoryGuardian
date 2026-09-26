// Adapted from WinMemoryCleaner Service/ComputerService.cs (Igor Mundstein and contributors).
// Modified 2026-09-26: manual working-set cleanup, exact NTSTATUS errors, checked privileges,
// privilege restoration, explicit before/after measurements. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using WinMemoryCleaner;

namespace MemoryOrb
{
    public enum OptimizationMode { Gentle, WorkingSetsAndCache, Full }
    public sealed class MemorySnapshot
    {
        public int Load { get; set; }
        public long Available { get; set; }
        public long Total { get; set; }
    }

    public sealed class OptimizationResult
    {
        public long AvailableChange { get; set; }
        public MemorySnapshot Before { get; set; }
        public MemorySnapshot After { get; set; }
        public OptimizationMode Mode { get; set; }
        public string Trigger { get; set; } = "手动点击";
        public List<string> Completed { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool Succeeded => Errors.Count == 0;
        public string Describe()
        {
            if (Completed.Count == 0) return "优化失败 · 右键查看结果";
            return (Errors.Count > 0 ? "部分完成 · " : "") + Describe(AvailableChange);
        }
        public string Details()
        {
            var text = new StringBuilder();
            text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine("触发方式：" + Trigger);
            text.AppendLine(Mode == OptimizationMode.Full ? "模式：一键完整清理（7 项）" : Mode == OptimizationMode.Gentle ? "模式：温和缓存清理" : "模式：工作集整理 + 温和缓存清理");
            text.AppendLine("内存占用：" + Before.Load + "% → " + After.Load + "%");
            text.AppendLine("可用内存：" + (Before.Available / 1073741824.0).ToString("0.00") + " GB → " + (After.Available / 1073741824.0).ToString("0.00") + " GB");
            text.AppendLine("可用内存变化：" + (AvailableChange / 1048576.0).ToString("+0.0;-0.0;0.0") + " MB");
            foreach (var step in Completed) text.AppendLine("完成：" + step);
            foreach (var error in Errors) text.AppendLine("失败：" + error);
            text.AppendLine("以上是系统实测差值，受其他程序影响。工作集整理不终止进程、不解除内存分配；再次访问数据时占用可能回升，也可能暂时变慢。");
            return text.ToString();
        }
        public void SaveReport()
        {
            Directory.CreateDirectory(OrbSettings.DirectoryPath);
            File.WriteAllText(Path.Combine(OrbSettings.DirectoryPath, "last-optimization.txt"), Details(), Encoding.UTF8);
        }
        public static string Describe(long bytes)
        {
            if (bytes < 1024 * 1024) return "清理完成 · 无明显变化";
            if (bytes >= 1024L * 1024 * 1024) return "可用 +" + (bytes / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
            return "可用 +" + (bytes / (1024.0 * 1024)).ToString("0") + " MB";
        }
    }

    public interface IMemoryBackend
    {
        MemorySnapshot Read();
        void PurgeLowPriorityStandby();
        void TrimWorkingSets();
        void TrimSystemFileCache();
        void FlushModifiedPages();
        void PurgeAllStandby();
        void CombinePages();
        void ReconcileRegistry();
        void FlushVolumeCaches();
    }

    public sealed class MemoryOptimizer
    {
        private readonly IMemoryBackend backend;
        private int running;
        public MemoryOptimizer(IMemoryBackend backend) { this.backend = backend; }
        public OptimizationResult Optimize(OptimizationMode mode = OptimizationMode.Gentle, Action<string> progress = null)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("正在优化，请稍候。");
            try
            {
                var result = new OptimizationResult { Before = backend.Read(), Mode = mode };
                if (mode == OptimizationMode.Full)
                {
                    // Match upstream's complete selection order. Full standby purge
                    // subsumes the mutually exclusive low-priority standby option.
                    RunStep(result, "1/7 进程工作集", backend.TrimWorkingSets, progress);
                    RunStep(result, "2/7 系统文件缓存", backend.TrimSystemFileCache, progress);
                    RunStep(result, "3/7 已修改页面", backend.FlushModifiedPages, progress);
                    RunStep(result, "4/7 全部待机缓存", backend.PurgeAllStandby, progress);
                    RunStep(result, "5/7 合并页面", backend.CombinePages, progress);
                    RunStep(result, "6/7 注册表缓存", backend.ReconcileRegistry, progress);
                    RunStep(result, "7/7 磁盘写入缓存", backend.FlushVolumeCaches, progress);
                }
                else
                {
                    if (mode == OptimizationMode.WorkingSetsAndCache)
                        RunStep(result, "进程工作集整理", backend.TrimWorkingSets, progress);
                    RunStep(result, "低优先级待机缓存", backend.PurgeLowPriorityStandby, progress);
                }
                result.After = backend.Read();
                result.AvailableChange = result.After.Available - result.Before.Available;
                return result;
            }
            finally { Interlocked.Exchange(ref running, 0); }
        }

        private static void RunStep(OptimizationResult result, string name, Action action, Action<string> progress)
        {
            if (progress != null) progress(name);
            try { action(); result.Completed.Add(name); }
            catch (Exception ex) { result.Errors.Add(name + "：" + ex.Message); }
        }
    }

    public sealed partial class WindowsMemoryBackend : IMemoryBackend
    {
        public MemorySnapshot Read()
        {
            var value = new Structs.Windows.MemoryStatusEx();
            if (!NativeMethods.GlobalMemoryStatusEx(value)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new MemorySnapshot { Load = value.MemoryLoad, Available = value.AvailPhys, Total = value.TotalPhys };
        }

        public void PurgeLowPriorityStandby()
        {
            SetMemoryListCommand(5); // MemoryPurgeLowPriorityStandbyList.
        }

        public void TrimWorkingSets()
        {
            SetMemoryListCommand(2); // MemoryEmptyWorkingSets, as used by upstream.
        }

        private static void SetMemoryListCommand(int command)
        {
            WithPrivilege("SeProfileSingleProcessPrivilege", () =>
                CheckNtStatus(NtSetSystemInformation(80, ref command, sizeof(int))));
        }

        private static void CheckNtStatus(int status)
        {
            if (status < 0)
                throw new Win32Exception((int)RtlNtStatusToDosError(status), "Windows 未完成操作 (NTSTATUS 0x" + status.ToString("X8") + ")。");
        }

        private static void WithPrivilege(string name, Action action)
        {
            using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges))
            {
                var state = new Structs.Windows.TokenPrivileges { Count = 1, Attr = 2 };
                if (!NativeMethods.LookupPrivilegeValue(null, name, ref state.Luid))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                Structs.Windows.TokenPrivileges previous;
                int returned;
                bool adjusted = AdjustTokenPrivileges(identity.Token, false, ref state,
                    Marshal.SizeOf(typeof(Structs.Windows.TokenPrivileges)), out previous, out returned);
                int error = Marshal.GetLastWin32Error();
                if (!adjusted || error != 0)
                    throw new Win32Exception(error, "需要管理员权限才能清理。请以管理员身份运行内存卫士。");
                try
                {
                    action();
                }
                finally
                {
                    NativeMethods.AdjustTokenPrivileges(identity.Token, false, ref previous, 0, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool disable,
            ref Structs.Windows.TokenPrivileges state, int length, out Structs.Windows.TokenPrivileges previous, out int returned);
        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int informationClass, ref int command, int length);
        [DllImport("ntdll.dll")]
        private static extern uint RtlNtStatusToDosError(int status);
    }
}
