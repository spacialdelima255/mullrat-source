namespace mullvad.Models
{
    public sealed class DriveEntry
    {
        public string RootPath    { get; set; } = string.Empty;
        public string Label       { get; set; } = string.Empty;
        public string DriveType   { get; set; } = string.Empty;
        public string FileSystem  { get; set; } = string.Empty;
        public long   TotalBytes  { get; set; }
        public long   FreeBytes   { get; set; }
        public bool   IsReady     { get; set; }
    }
}
