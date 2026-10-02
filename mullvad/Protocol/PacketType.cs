namespace mullvad.Protocol
{
    public enum PacketType : byte
    {
        Handshake  = 0x01,
        Ping       = 0x02,
        Pong       = 0x03,
        Command    = 0x10,
        Response   = 0x11,
        Disconnect = 0xFF,
    }
}
