namespace mullvad.Models
{
    public sealed class FileChunk
    {
        public string TransferId { get; set; } = string.Empty;
        public string FilePath   { get; set; } = string.Empty;
        public long   FileSize   { get; set; }
        public long   Offset     { get; set; }
        public byte[] Data       { get; set; } = System.Array.Empty<byte>();
        public bool   IsLast     { get; set; }
    }
}
