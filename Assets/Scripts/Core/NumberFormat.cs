using System;
using System.Globalization;

namespace StartupClicker.Core
{
    /// <summary>Turns big numbers into short readable strings: 1234 -> "1,234", 1234567 -> "1.23M".</summary>
    public static class NumberFormat
    {
        static readonly string[] Suffixes =
            { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <param name="value">The number to show.</param>
        /// <param name="smallDecimals">Decimals to keep for values under 1000 (e.g. 1 for "0.1/sec").</param>
        public static string Format(double value, int smallDecimals = 0)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "∞";
            if (value < 0) return "-" + Format(-value, smallDecimals);

            if (value < 1000000)
            {
                if (smallDecimals > 0 && value < 1000 && Math.Abs(value - Math.Round(value)) > 1e-9)
                    return value.ToString("N" + smallDecimals, Inv);
                return Math.Floor(value + 1e-9).ToString("N0", Inv);
            }

            int tier = (int)Math.Floor(Math.Log10(value) / 3);
            if (tier >= Suffixes.Length) return value.ToString("0.00e0", Inv);

            double scaled = value / Math.Pow(1000, tier);
            // Truncate rather than round so the display never shows more than the player has.
            scaled = Math.Floor(scaled * 100 + 1e-9) / 100;
            return scaled.ToString("0.##", Inv) + Suffixes[tier];
        }
    }
}
