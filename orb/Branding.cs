// Earth Guardian, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;

namespace MemoryOrb
{
    public static class Branding
    {
        public const string Name = "内存卫士";
        public const string AuthorHomepage = "https://blog.csdn.net/syq10086?type=blog";
        public const string DownloadPage = "https://www.xiaopuwa.com/earth-guardian/";
        public static string Version => typeof(Branding).Assembly.GetName().Version.ToString(3);
        public static ProcessStartInfo CreateDownloadRequest()
        {
            return new ProcessStartInfo(DownloadPage) { UseShellExecute = true };
        }
        public static ProcessStartInfo CreateHomepageRequest()
        {
            return new ProcessStartInfo(AuthorHomepage) { UseShellExecute = true };
        }
    }
}
