// MemoryOrb modifications, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace MemoryOrb
{
    public class OrbSettings
    {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public int Diameter { get; set; } = 72;
        public bool AlwaysOnTop { get; set; } = true;
        public bool AutoOptimize { get; set; } = true;
        public int AutoThreshold { get; set; } = 80;
        public int AutoCooldownMinutes { get; set; } = 10;
        public OptimizationMode AutoMode { get; set; } = OptimizationMode.WorkingSetsAndCache;
        public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoryOrb");
        public static string FilePath => Path.Combine(DirectoryPath, "settings.xml");

        public void Validate()
        {
            if (Diameter != 56 && Diameter != 72 && Diameter != 88) Diameter = 72;
            if (double.IsInfinity(Left)) Left = double.NaN;
            if (double.IsInfinity(Top)) Top = double.NaN;
            if (AutoThreshold < 60 || AutoThreshold > 95) AutoThreshold = 80;
            if (AutoCooldownMinutes != 5 && AutoCooldownMinutes != 10 && AutoCooldownMinutes != 15 && AutoCooldownMinutes != 30) AutoCooldownMinutes = 10;
            if (!Enum.IsDefined(typeof(OptimizationMode), AutoMode)) AutoMode = OptimizationMode.WorkingSetsAndCache;
        }

        public static OrbSettings Load(string path)
        {
            try
            {
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                {
                    var value = (OrbSettings)new XmlSerializer(typeof(OrbSettings)).Deserialize(reader);
                    value.Validate();
                    return value;
                }
            }
            catch (IOException) { return new OrbSettings(); }
            catch (UnauthorizedAccessException) { return new OrbSettings(); }
            catch (InvalidOperationException) { return new OrbSettings(); }
            catch (XmlException) { return new OrbSettings(); }
        }

        public void Save(string path)
        {
            Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var writer = new StreamWriter(temporary, false, System.Text.Encoding.UTF8))
                new XmlSerializer(typeof(OrbSettings)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }

    public sealed class OptimizationGate
    {
        private DateTime? pressureSince;
        private DateTime lastAttempt = DateTime.MinValue;
        private DateTime lastSample = DateTime.MinValue;
        public void ResetPressure() { pressureSince = null; }
        public int CooldownSeconds(DateTime now, int cooldownMinutes)
        {
            return (int)Math.Max(0, Math.Ceiling((TimeSpan.FromMinutes(cooldownMinutes) - (now - lastAttempt)).TotalSeconds));
        }
        public int PressureSecondsRemaining(DateTime now)
        {
            return pressureSince.HasValue ? (int)Math.Max(0, Math.Ceiling(30 - (now - pressureSince.Value).TotalSeconds)) : 30;
        }
        public bool ShouldRun(bool enabled, bool busy, int load, DateTime now, int threshold = 80, int cooldownMinutes = 10)
        {
            // A missed polling interval (sleep/resume or a blocked UI) is not proof
            // of sustained memory pressure. Start observing again after the gap.
            if (lastSample != DateTime.MinValue && (now < lastSample || now - lastSample > TimeSpan.FromSeconds(8))) ResetPressure();
            lastSample = now;
            if (!enabled || busy || load < threshold || load > 100) { ResetPressure(); return false; }
            if (!pressureSince.HasValue) pressureSince = now;
            return now - pressureSince.Value >= TimeSpan.FromSeconds(30) && CooldownSeconds(now, cooldownMinutes) == 0;
        }
        public void RecordAttempt(DateTime now) { lastAttempt = now; pressureSince = null; }

        public AutoCleanupStatus GetStatus(bool enabled, bool demo, bool busy, int load, DateTime now, int threshold, int cooldownMinutes)
        {
            if (demo) return new AutoCleanupStatus("演示", "演示模式不执行自动清理");
            if (busy) return new AutoCleanupStatus("清理中", "正在清理，请稍候");
            if (!enabled) return new AutoCleanupStatus("手动模式", "自动清理已关闭");
            if (load < 0 || load > 100) return new AutoCleanupStatus("读取失败", "内存读取失败，暂停自动检测");
            int cooldown = CooldownSeconds(now, cooldownMinutes);
            if (cooldown > 0)
                return new AutoCleanupStatus("冷却 " + (int)Math.Ceiling(cooldown / 60.0) + "分",
                    "自动清理冷却：剩余 " + (cooldown / 60) + "分" + (cooldown % 60).ToString("00") + "秒（上次清理 " + lastAttempt.ToLocalTime().ToString("HH:mm:ss") + "）");
            if (load < threshold)
                return new AutoCleanupStatus("自动守护", "监测中：" + load + "% / 阈值 " + threshold + "%（需持续 30 秒）");
            int remaining = PressureSecondsRemaining(now);
            return new AutoCleanupStatus(remaining > 0 ? remaining + "秒后清理" : "准备清理",
                "已达阈值 " + threshold + "%：" + (remaining > 0 ? "持续超标再等 " + remaining + " 秒后自动清理" : "正在触发自动清理"));
        }
    }

    public sealed class AutoCleanupStatus
    {
        public string ShortText { get; }
        public string Details { get; }
        public AutoCleanupStatus(string shortText, string details) { ShortText = shortText; Details = details; }
    }
}
