using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.RegistryEditor
{
    public sealed class RegistryEditorClientModule
    {
        public static string ModuleId => "mullvad.registryeditor";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "list_keys":    return ListKeys(payload);
                    case "list_values":  return ListValues(payload);
                    case "create_key":   return CreateKey(payload);
                    case "delete_key":   return DeleteKey(payload);
                    case "set_value":    return SetValue(payload);
                    case "delete_value": return DeleteValue(payload);
                    default:             return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Actions ──────────────────────────────────────────────────────────

        private static string ListKeys(string payload)
        {
            var path = GetStr(payload, "path");
            if (path == null) return Err("path required");

            using var key = OpenKey(path, false);
            if (key == null) return "[]";

            var sb = new StringBuilder("[");
            bool first = true;
            try
            {
                foreach (var name in key.GetSubKeyNames())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Json(name));
                }
            }
            catch { }
            sb.Append("]");
            return sb.ToString();
        }

        private static string ListValues(string payload)
        {
            var path = GetStr(payload, "path");
            if (path == null) return Err("path required");

            using var key = OpenKey(path, false);
            if (key == null) return "[]";

            var sb = new StringBuilder("[");
            bool first = true;
            try
            {
                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        if (!first) sb.Append(',');
                        first = false;

                        var kind = key.GetValueKind(name);
                        var raw  = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        EncodeValue(kind, raw, out var typeStr, out var dataStr);

                        sb.Append("{\"name\":").Append(Json(name))
                          .Append(",\"type\":").Append(Json(typeStr))
                          .Append(",\"data\":").Append(Json(dataStr))
                          .Append("}");
                    }
                    catch { }
                }
            }
            catch { }
            sb.Append("]");
            return sb.ToString();
        }

        private static string CreateKey(string payload)
        {
            var path = GetStr(payload, "path");
            if (path == null) return Err("path required");

            int slash = path.LastIndexOf('\\');
            if (slash < 0) return Err("invalid path");

            var parentPath = path.Substring(0, slash);
            var newName    = path.Substring(slash + 1);
            if (string.IsNullOrEmpty(newName)) return Err("key name is empty");

            using var parent = OpenKey(parentPath, true);
            if (parent == null) return Err("parent key not found or access denied");

            using var created = parent.CreateSubKey(newName);
            if (created == null) return Err("failed to create key");

            return "{\"success\":true}";
        }

        private static string DeleteKey(string payload)
        {
            var path = GetStr(payload, "path");
            if (path == null) return Err("path required");

            int slash = path.LastIndexOf('\\');
            if (slash < 0) return Err("invalid path");

            var parentPath = path.Substring(0, slash);
            var keyName    = path.Substring(slash + 1);

            using var parent = OpenKey(parentPath, true);
            if (parent == null) return Err("parent key not found or access denied");

            try { parent.DeleteSubKeyTree(keyName, false); }
            catch (Exception ex) { return Err(ex.Message); }

            return "{\"success\":true}";
        }

        private static string SetValue(string payload)
        {
            var path = GetStr(payload, "path");
            var name = GetStr(payload, "name") ?? "";
            var type = GetStr(payload, "type") ?? "REG_SZ";
            var data = GetStr(payload, "data") ?? "";

            if (path == null) return Err("path required");

            using var key = OpenKey(path, true);
            if (key == null) return Err("key not found or access denied");

            try
            {
                switch (type)
                {
                    case "REG_SZ":
                        key.SetValue(name, data, RegistryValueKind.String);
                        break;
                    case "REG_EXPAND_SZ":
                        key.SetValue(name, data, RegistryValueKind.ExpandString);
                        break;
                    case "REG_DWORD":
                        if (!uint.TryParse(data, out uint dword)) return Err("invalid DWORD");
                        key.SetValue(name, (int)dword, RegistryValueKind.DWord);
                        break;
                    case "REG_QWORD":
                        if (!ulong.TryParse(data, out ulong qword)) return Err("invalid QWORD");
                        key.SetValue(name, (long)qword, RegistryValueKind.QWord);
                        break;
                    case "REG_BINARY":
                        var hex = data.Replace(" ", "").Replace("-", "");
                        if (hex.Length % 2 != 0) return Err("odd hex length");
                        var bytes = new byte[hex.Length / 2];
                        for (int i = 0; i < bytes.Length; i++)
                            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                        key.SetValue(name, bytes, RegistryValueKind.Binary);
                        break;
                    case "REG_MULTI_SZ":
                        var lines = data.Split(new[] { "\n" }, StringSplitOptions.None);
                        key.SetValue(name, lines, RegistryValueKind.MultiString);
                        break;
                    default:
                        return Err("unknown type: " + type);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }

            return "{\"success\":true}";
        }

        private static string DeleteValue(string payload)
        {
            var path = GetStr(payload, "path");
            var name = GetStr(payload, "name") ?? "";

            if (path == null) return Err("path required");

            using var key = OpenKey(path, true);
            if (key == null) return Err("key not found or access denied");

            try { key.DeleteValue(name, false); }
            catch (Exception ex) { return Err(ex.Message); }

            return "{\"success\":true}";
        }

        // ── Registry helpers ─────────────────────────────────────────────────

        private static RegistryKey OpenKey(string path, bool writable)
        {
            var remaining = path;
            var hive = ExtractHive(ref remaining);
            if (hive == null) return null;

            if (string.IsNullOrEmpty(remaining))
                return hive;

            return hive.OpenSubKey(remaining, writable);
        }

        private static RegistryKey ExtractHive(ref string path)
        {
            var u = path.ToUpperInvariant();
            string[] longPrefixes  = { "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER", "HKEY_CLASSES_ROOT", "HKEY_USERS", "HKEY_CURRENT_CONFIG" };
            string[] shortPrefixes = { "HKLM",               "HKCU",              "HKCR",              "HKU",        "HKCC"               };
            RegistryKey[] hives    = { Registry.LocalMachine, Registry.CurrentUser, Registry.ClassesRoot, Registry.Users, Registry.CurrentConfig };

            for (int i = 0; i < longPrefixes.Length; i++)
            {
                if (u == longPrefixes[i] || u == shortPrefixes[i])
                {
                    path = "";
                    return hives[i];
                }
                if (u.StartsWith(longPrefixes[i] + "\\"))
                {
                    path = path.Substring(longPrefixes[i].Length + 1);
                    return hives[i];
                }
                if (u.StartsWith(shortPrefixes[i] + "\\"))
                {
                    path = path.Substring(shortPrefixes[i].Length + 1);
                    return hives[i];
                }
            }
            return null;
        }

        private static void EncodeValue(RegistryValueKind kind, object value,
            out string typeStr, out string dataStr)
        {
            switch (kind)
            {
                case RegistryValueKind.String:
                    typeStr = "REG_SZ";
                    dataStr = value as string ?? "";
                    break;
                case RegistryValueKind.ExpandString:
                    typeStr = "REG_EXPAND_SZ";
                    dataStr = value as string ?? "";
                    break;
                case RegistryValueKind.DWord:
                    typeStr = "REG_DWORD";
                    dataStr = Convert.ToUInt32(value).ToString();
                    break;
                case RegistryValueKind.QWord:
                    typeStr = "REG_QWORD";
                    dataStr = Convert.ToUInt64(value).ToString();
                    break;
                case RegistryValueKind.Binary:
                    typeStr = "REG_BINARY";
                    var b   = value as byte[];
                    dataStr = b != null ? BitConverter.ToString(b).Replace("-", " ") : "";
                    break;
                case RegistryValueKind.MultiString:
                    typeStr = "REG_MULTI_SZ";
                    var parts = value as string[];
                    dataStr = parts != null ? string.Join("\n", parts) : "";
                    break;
                default:
                    typeStr = "REG_UNKNOWN";
                    dataStr = value?.ToString() ?? "";
                    break;
            }
        }

        // ── JSON helpers ─────────────────────────────────────────────────────

        private static string GetStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var k   = "\"" + key + "\"";
            int idx = json.IndexOf(k);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return null;
            int start = colon + 1;
            while (start < json.Length && json[start] == ' ') start++;
            if (start >= json.Length || json[start] != '"') return null;
            start++;
            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
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

        private static string Err(string msg)  => "{\"error\":" + Json(msg) + "}";

        private static string Json(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else          sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
