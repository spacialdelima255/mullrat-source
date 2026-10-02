using System.Collections.Concurrent;
using mullvad.Network;
using mullvad.Protocol;

namespace mullvad.Modules;

internal static class ModuleDelivery
{
    private const int ChunkSize = 256 * 1024; // 256 KB per chunk

    // Deliver a module DLL to the client.  Returns true when the client acks success.
    // If the module is already loaded on the client, returns true immediately.
    public static async Task<bool> EnsureDeliveredAsync(
        ClientHandler   handler,
        string          moduleFileName,
        byte[]          dllBytes,
        IProgress<string>? progress = null,
        CancellationToken   ct      = default)
    {
        var moduleId = DeriveModuleId(moduleFileName);

        if (handler.LoadedModules.Contains(moduleId))
            return true;

        var chunks = SplitChunks(dllBytes);
        var total  = chunks.Length;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Action<ClientHandler, Packet>? listener = null;
        listener = (_, pkt) =>
        {
            if (pkt.Type != PacketType.ModuleLoadAck) return;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(pkt.Json).RootElement;
                if (!doc.TryGetProperty("module_id", out var mid) || mid.GetString() != moduleId)
                    return;

                var ok = doc.TryGetProperty("success", out var s) && s.GetBoolean();
                handler.PacketReceived -= listener;
                tcs.TrySetResult(ok);
            }
            catch { }
        };
        handler.PacketReceived += listener;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(30));
        linked.Token.Register(() =>
        {
            handler.PacketReceived -= listener;
            tcs.TrySetCanceled(linked.Token);
        });

        for (int i = 0; i < total; i++)
        {
            progress?.Report($"Sending chunk {i + 1}/{total}…");
            await handler.SendAsync(Packet.Create(PacketType.ModuleLoad, new
            {
                module_id    = moduleId,
                chunk_index  = i,
                total_chunks = total,
                data         = Convert.ToBase64String(chunks[i]),
            }));
        }

        progress?.Report("Waiting for module load confirmation…");

        bool success;
        try   { success = await tcs.Task; }
        catch { handler.PacketReceived -= listener; return false; }

        if (success)
            handler.LoadedModules.Add(moduleId);

        return success;
    }

    // "mullvad.Module.FileManager" → "mullvad.filemanager"
    public static string DeriveModuleId(string moduleFileName)
    {
        var last = moduleFileName.Split('.').LastOrDefault() ?? moduleFileName;
        return "mullvad." + last.ToLowerInvariant();
    }

    private static byte[][] SplitChunks(byte[] data)
    {
        var list = new List<byte[]>();
        for (int off = 0; off < data.Length; off += ChunkSize)
        {
            var len   = Math.Min(ChunkSize, data.Length - off);
            var chunk = new byte[len];
            Array.Copy(data, off, chunk, 0, len);
            list.Add(chunk);
        }
        if (list.Count == 0) list.Add(Array.Empty<byte>());
        return list.ToArray();
    }
}
