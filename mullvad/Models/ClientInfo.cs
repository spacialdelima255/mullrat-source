namespace mullvad.Models
{
    public class ClientInfo
    {
        public string Id           { get; } = Guid.NewGuid().ToString("N")[..8];
        public string Computer     { get; set; } = "Unknown";
        public string Username     { get; set; } = "Unknown";
        public string IpAddress    { get; set; } = "";
        public int    Port         { get; set; }
        public string Os           { get; set; } = "";
        public string OsEdition    { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string Country      { get; set; } = "xx";   // ISO 3166-1 alpha-2 lowercase
        public string Group        { get; set; } = "Default";
        public string Version      { get; set; } = "";
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;
        public bool IsConnected    { get; set; } = true;

        public string DisplayIp  => $"{IpAddress}:{Port}";
        public string Uptime     => (DateTime.UtcNow - ConnectedAt).ToString(@"hh\:mm\:ss");
        public string InstalledAt => ConnectedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}
