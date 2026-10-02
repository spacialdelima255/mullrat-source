using System;

namespace mullvad.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixSeconds(this DateTime dt) =>
            (long)(dt.ToUniversalTime() - _epoch).TotalSeconds;

        public static long ToUnixMilliseconds(this DateTime dt) =>
            (long)(dt.ToUniversalTime() - _epoch).TotalMilliseconds;

        public static DateTime FromUnixSeconds(long seconds) =>
            _epoch.AddSeconds(seconds);

        public static DateTime FromUnixMilliseconds(long ms) =>
            _epoch.AddMilliseconds(ms);

        public static string ToDisplayString(this DateTime dt) =>
            dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public static string ToDisplayStringUtc(this DateTime dt) =>
            dt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
    }
}
