using mullvad.Enums;

namespace mullvad.Models
{
    public sealed class FileEntry
    {
        public string Name         { get; set; } = string.Empty;
        public string FullPath     { get; set; } = string.Empty;
        public FileEntryType Type  { get; set; } = FileEntryType.File;
        public long   SizeBytes    { get; set; }
        public long   CreatedUtc   { get; set; }
        public long   ModifiedUtc  { get; set; }
        public long   AccessedUtc  { get; set; }
        public string Attributes   { get; set; } = string.Empty;
    }
}
