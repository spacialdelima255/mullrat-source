using System.Collections.Concurrent;
using mullvad.Network;
using mullvad.Protocol;

namespace mullvad.Modules;

// Per-module-per-client context.  Create one instance per module form, dispose on close.
internal sealed class ModuleContext : IDisposable
{
    private readonly ClientHandler _handler;
    private readonly string        _moduleId;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

    public event EventHandler? Disconnected;

    public ModuleContext(ClientHandler handler, string moduleId)
    {
        _handler  = handler;
        _moduleId = moduleId;
        handler.PacketReceived += OnPacket;
        handler.Disconnected   += OnDisconnected;
    }

    // Execute a module action and wait for the result (JSON string from the client module).
    public async Task<string> ExecuteAsync(
        string action,
        string payload      = "",
        CancellationToken ct = default,
        TimeSpan? timeout    = null)
    {
        var reqId = Guid.NewGuid().ToString("N")[..12];
        var tcs   = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[reqId] = tcs;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
        linked.Token.Register(() =>
        {
            _pending.TryRemove(reqId, out _);
            tcs.TrySetCanceled(linked.Token);
        });

        await _handler.SendAsync(Packet.Create(PacketType.ModuleExecute, new
        {
            module_id  = _moduleId,
            request_id = reqId,
            action,
            payload,
        }));

        return await tcs.Task;
    }

    private void OnPacket(ClientHandler _, Packet pkt)
    {
        if (pkt.Type != PacketType.ModuleData) return;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(pkt.Json).RootElement;

            if (!doc.TryGetProperty("module_id",  out var mid) || mid.GetString() != _moduleId) return;
            if (!doc.TryGetProperty("request_id", out var rid)) return;

            var reqId = rid.GetString();
            if (reqId is null) return;

            if (!_pending.TryRemove(reqId, out var tcs)) return;

            doc.TryGetProperty("data", out var data);
            tcs.TrySetResult(data.GetString() ?? "");
        }
        catch { }
    }

    private void OnDisconnected(ClientHandler _)
    {
        foreach (var tcs in _pending.Values) tcs.TrySetCanceled();
        _pending.Clear();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _handler.PacketReceived -= OnPacket;
        _handler.Disconnected   -= OnDisconnected;
        foreach (var tcs in _pending.Values) tcs.TrySetCanceled();
        _pending.Clear();
    }
}
