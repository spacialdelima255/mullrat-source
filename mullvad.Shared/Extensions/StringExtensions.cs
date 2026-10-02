using System;
using System.Text;

namespace mullvad.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength, string suffix = "…")
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            int cut = Math.Max(0, maxLength - suffix.Length);
            return value.Substring(0, cut) + suffix;
        }

        public static string Repeat(this string value, int count)
        {
            if (string.IsNullOrEmpty(value) || count <= 0) return string.Empty;
            var sb = new StringBuilder(value.Length * count);
            for (int i = 0; i < count; i++) sb.Append(value);
            return sb.ToString();
        }

        public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);

        public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);

        public static string ToHumanSize(this long bytes)
        {
            if (bytes < 0)            return "-";
            if (bytes < 1024L)        return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
