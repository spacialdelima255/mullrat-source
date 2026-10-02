namespace mullvad.Models
{
    public sealed class StartupEntry
    {
        public string Name      { get; set; } = string.Empty;
        public string Path      { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string Location  { get; set; } = string.Empty;
        public bool   Enabled   { get; set; }
    }
}
