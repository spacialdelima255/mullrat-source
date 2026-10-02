namespace mullvad.Enums
{
    /// <summary>TCP connection states mirroring the RFC 793 state machine.</summary>
    public enum ConnectionState
    {
        Unknown = 0,
        Closed,
        Listen,
        SynSent,
        SynReceived,
        Established,
        FinWait1,
        FinWait2,
        CloseWait,
        Closing,
        LastAck,
        TimeWait,
        DeleteTcb
    }
}
