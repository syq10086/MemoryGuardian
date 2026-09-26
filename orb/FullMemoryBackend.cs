// Adapted from WinMemoryCleaner ComputerService.cs (Igor Mundstein and contributors).
// Modified 2026-09-26: checked NTSTATUS, pointer-sized structures, privilege restoration,
// documented volume flushing with per-drive errors. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using WinMemoryCleaner;

namespace MemoryOrb
{
    public sealed partial class WindowsMemoryBackend
    {
        public void FlushModifiedPages() { SetMemoryListCommand(3); }
        public void PurgeAllStandby() { SetMemoryListCommand(4); }

        public void TrimSystemFileCache()
        {
            WithPrivilege("SeIncreaseQuotaPrivilege", () =>
            {
                // Documented equivalent of trimming the system file cache, also
                // used by upstream. Pointer-sized -1 works in either process bitness.
                if (!NativeMethods.SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            });
        }

        public void CombinePages()
        {
            WithPrivilege("SeProfileSingleProcessPrivilege", () =>
            {
                var data = new CombineInformation();
                int size = Marshal.SizeOf(typeof(CombineInformation));
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(data, buffer, false);
                    CheckNtStatus(NativeMethods.NtSetSystemInformation(130, buffer, (uint)size));
                }
                finally { Marshal.FreeHGlobal(buffer); }
            });
        }

        public void ReconcileRegistry()
        {
            CheckNtStatus(NativeMethods.NtSetSystemInformation(155, IntPtr.Zero, 0));
        }

        public void FlushVolumeCaches()
        {
            var errors = new List<string>();
            int attempted = 0, completed = 0;
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed) continue;
                    attempted++;
                    // Flush queued writes, never discard unwritten data. Upstream's
                    // optional IOCTL cache-discard/write-order hints are not copied.
                    using (var handle = NativeMethods.CreateFile(@"\\.\" + drive.Name.TrimEnd('\\'),
                        FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero))
                    {
                        if (handle.IsInvalid || !NativeMethods.FlushFileBuffers(handle))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    completed++;
                }
                catch (Exception ex) { errors.Add(drive.Name + "：" + ex.Message); }
            }
            if (attempted == 0) throw new InvalidOperationException("没有可刷新缓存的固定磁盘。");
            if (errors.Count > 0)
                throw new IOException("磁盘刷新完成 " + completed + "/" + attempted + "；" + string.Join("；", errors));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CombineInformation
        {
            public IntPtr Handle;
            public UIntPtr PagesCombined;
            public uint Flags;
        }
    }
}
