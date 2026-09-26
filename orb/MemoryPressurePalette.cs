// Memory Guardian, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Windows.Media;

namespace MemoryOrb
{
    public static class MemoryPressurePalette
    {
        // 0–50% stays blue. Above 50%, move through warm colors to red at 95%.
        // Quantize to 21 cached textures to avoid per-frame image allocations.
        public static int Band(int load) => (int)Math.Round(Math.Max(0, Math.Min(1, (load - 50) / 45.0)) * 20);
        public static Color Surface(bool land, int band)
        {
            double t = Math.Max(0, Math.Min(20, band)) / 20.0;
            Color low = land ? Color.FromRgb(106, 191, 206) : Color.FromRgb(20, 82, 151);
            Color middle = land ? Color.FromRgb(255, 201, 124) : Color.FromRgb(152, 85, 49);
            Color high = land ? Color.FromRgb(255, 122, 106) : Color.FromRgb(155, 29, 45);
            return t <= .5 ? Mix(low, middle, t * 2) : Mix(middle, high, (t - .5) * 2);
        }
        private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
