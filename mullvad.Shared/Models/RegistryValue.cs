namespace mullvad.Models
{
    public sealed class RegistryValue
    {
        public string Name    { get; set; } = string.Empty;
        public string Kind    { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public byte[] RawData { get; set; } = System.Array.Empty<byte>();
    }
}
