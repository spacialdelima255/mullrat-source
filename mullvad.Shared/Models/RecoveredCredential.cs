namespace mullvad.Models
{
    public sealed class RecoveredCredential
    {
        public string Username    { get; set; } = string.Empty;
        public string Password    { get; set; } = string.Empty;
        public string Url         { get; set; } = string.Empty;
        public string Application { get; set; } = string.Empty;
        public string Type        { get; set; } = string.Empty;
        public long   CapturedUtc { get; set; }
    }
}
