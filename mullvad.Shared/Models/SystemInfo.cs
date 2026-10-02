namespace mullvad.Models
{
    public sealed class SystemInfo
    {
        public string OsCaption   { get; set; } = string.Empty;
        public string OsBuild     { get; set; } = string.Empty;
        public string CpuName     { get; set; } = string.Empty;
        public int    CpuCores    { get; set; }
        public int    CpuThreads  { get; set; }
        public long   RamTotalBytes  { get; set; }
        public long   RamFreeBytes   { get; set; }
        public string GpuName     { get; set; } = string.Empty;
        public string DotNetVersion { get; set; } = string.Empty;
        public long   UptimeSeconds { get; set; }
        public string SystemRoot  { get; set; } = string.Empty;
    }
}
