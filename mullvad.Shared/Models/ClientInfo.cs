using mullvad.Enums;

namespace mullvad.Models
{
    public sealed class ClientInfo
    {
        public string ClientId    { get; set; } = string.Empty;
        public string Hostname    { get; set; } = string.Empty;
        public string Username    { get; set; } = string.Empty;
        public string OsVersion   { get; set; } = string.Empty;
        public PlatformArch Arch  { get; set; } = PlatformArch.Unknown;
        public PrivilegeLevel Privilege { get; set; } = PrivilegeLevel.Unknown;
        public ClientStatus Status      { get; set; } = ClientStatus.Unknown;
        public string Tag         { get; set; } = string.Empty;
        public string Version     { get; set; } = string.Empty;
        public long ConnectedAtUtc { get; set; }
        public long LastSeenUtc    { get; set; }
        public string LocalAddress  { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
    }
}
