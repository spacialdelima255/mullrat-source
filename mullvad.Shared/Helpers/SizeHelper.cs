namespace mullvad.Helpers
{
    public static class SizeHelper
    {
        private static readonly string[] _units = { "B", "KB", "MB", "GB", "TB", "PB" };

        public static string Format(long bytes, int decimals = 1)
        {
            if (bytes < 0) return "-";
            double val = bytes;
            int    unit = 0;
            while (val >= 1024 && unit < _units.Length - 1) { val /= 1024; unit++; }
            return unit == 0
                ? $"{(long)val} {_units[unit]}"
                : $"{val.ToString($"F{decimals}")} {_units[unit]}";
        }

        /// <summary>Returns how many full pages a byte count occupies.</summary>
        public static long ToPages(long bytes, int pageSize = 4096) =>
            (bytes + pageSize - 1) / pageSize;
    }
}
