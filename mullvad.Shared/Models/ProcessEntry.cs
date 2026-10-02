namespace mullvad.Models
{
    public sealed class ProcessEntry
    {
        public int    Pid         { get; set; }
        public int    ParentPid   { get; set; }
        public string Name        { get; set; } = string.Empty;
        public string Path        { get; set; } = string.Empty;
        public string MainWindowTitle { get; set; } = string.Empty;
        public long   WorkingSetBytes  { get; set; }
        public long   PrivateBytesUsed { get; set; }
        public double CpuPercent  { get; set; }
        public string Username    { get; set; } = string.Empty;
        public bool   IsElevated  { get; set; }
    }
}
