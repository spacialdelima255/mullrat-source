using mullvad.Enums;

namespace mullvad.Models
{
    public sealed class NetworkConnection
    {
        public string  Protocol       { get; set; } = string.Empty;
        public string  LocalAddress   { get; set; } = string.Empty;
        public int     LocalPort      { get; set; }
        public string  RemoteAddress  { get; set; } = string.Empty;
        public int     RemotePort     { get; set; }
        public ConnectionState State  { get; set; } = ConnectionState.Unknown;
        public int     OwnerPid       { get; set; }
        public string  OwnerProcess   { get; set; } = string.Empty;
    }
}
