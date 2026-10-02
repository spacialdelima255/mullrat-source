using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace mullvad.Module.TcpConnections
{
    public sealed class TcpConnectionsClientModule
    {
        public static string ModuleId => "mullvad.tcpconnections";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "collect": return Collect();
                    case "close":   return Close(payload);
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ─── P/Invoke ────────────────────────────────────────────────────────────

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwSize,
            bool bOrder,
            int ulAf,        // AF_INET = 2
            int TableClass,  // TCP_TABLE_OWNER_PID_ALL = 5
            int Reserved);

        [DllImport("iphlpapi.dll")]
        private static extern uint SetTcpEntry(IntPtr pTcpRow);

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcprowOwnerPid
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcprow   // for SetTcpEntry
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
        }

        private const int AF_INET              = 2;
        private const int TCP_TABLE_OWNER_PID  = 5;   // TCP_TABLE_OWNER_PID_ALL
        private const uint ERROR_INSUFFICIENT_BUFFER = 122;

        // ─── Collect ─────────────────────────────────────────────────────────────

        private static string Collect()
        {
            var rows = GetTable();
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var row in rows)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{')
                  .Append("\"process\":").Append(Json(GetProcessName(row.dwOwningPid))).Append(',')
                  .Append("\"local_address\":").Append(Json(ToAddress(row.dwLocalAddr))).Append(',')
                  .Append("\"local_port\":").Append(Ntohs(row.dwLocalPort)).Append(',')
                  .Append("\"remote_address\":").Append(Json(ToAddress(row.dwRemoteAddr))).Append(',')
                  .Append("\"remote_port\":").Append(Ntohs(row.dwRemotePort)).Append(',')
                  .Append("\"state\":").Append(Json(StateName((int)row.dwState)))
                  .Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        // ─── Close ───────────────────────────────────────────────────────────────

        private static string Close(string payload)
        {
            // Parse: {"local_address":"...","local_port":N,"remote_address":"...","remote_port":N}
            string localAddr  = ParseJsonString(payload, "local_address")  ?? "";
            string remoteAddr = ParseJsonString(payload, "remote_address") ?? "";
            int    localPort  = ParseJsonInt(payload, "local_port");
            int    remotePort = ParseJsonInt(payload, "remote_port");

            if (string.IsNullOrEmpty(localAddr))
                return Err("Missing local_address");

            var rows = GetTable();
            MibTcprowOwnerPid? match = null;
            foreach (var row in rows)
            {
                if (ToAddress(row.dwLocalAddr)  == localAddr  &&
                    Ntohs(row.dwLocalPort)       == localPort  &&
                    ToAddress(row.dwRemoteAddr)  == remoteAddr &&
                    Ntohs(row.dwRemotePort)      == remotePort)
                {
                    match = row;
                    break;
                }
            }

            if (match == null)
                return Err("Connection not found");

            var tcpRow = new MibTcprow
            {
                dwState      = 12,  // DELETE_TCB
                dwLocalAddr  = match.Value.dwLocalAddr,
                dwLocalPort  = match.Value.dwLocalPort,
                dwRemoteAddr = match.Value.dwRemoteAddr,
                dwRemotePort = match.Value.dwRemotePort
            };

            int size = Marshal.SizeOf(tcpRow);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(tcpRow, ptr, false);
                uint result = SetTcpEntry(ptr);
                if (result != 0)
                    return Err("SetTcpEntry failed with code " + result);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return "{\"success\":true}";
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static MibTcprowOwnerPid[] GetTable()
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID, 0);

            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                uint ret = GetExtendedTcpTable(buf, ref size, true, AF_INET, TCP_TABLE_OWNER_PID, 0);
                if (ret != 0)
                    throw new InvalidOperationException("GetExtendedTcpTable failed: " + ret);

                int count = Marshal.ReadInt32(buf);
                int rowSize = Marshal.SizeOf(typeof(MibTcprowOwnerPid));
                var rows = new MibTcprowOwnerPid[count];
                IntPtr cursor = new IntPtr(buf.ToInt64() + 4);
                for (int i = 0; i < count; i++)
                {
                    rows[i] = (MibTcprowOwnerPid)Marshal.PtrToStructure(cursor, typeof(MibTcprowOwnerPid));
                    cursor = new IntPtr(cursor.ToInt64() + rowSize);
                }
                return rows;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        private static string ToAddress(uint addr)
            => new IPAddress(addr).ToString();

        private static int Ntohs(uint port)
        {
            // port is stored in network byte order in the low 16 bits
            byte hi = (byte)((port >> 8) & 0xFF);
            byte lo = (byte)(port & 0xFF);
            return (lo << 8) | hi;
        }

        private static string GetProcessName(uint pid)
        {
            try
            {
                var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return "PID: " + pid;
            }
        }

        private static string StateName(int state)
        {
            switch (state)
            {
                case  1: return "CLOSED";
                case  2: return "LISTEN";
                case  3: return "SYN_SENT";
                case  4: return "SYN_RECEIVED";
                case  5: return "ESTABLISHED";
                case  6: return "FIN_WAIT1";
                case  7: return "FIN_WAIT2";
                case  8: return "CLOSE_WAIT";
                case  9: return "CLOSING";
                case 10: return "LAST_ACK";
                case 11: return "TIME_WAIT";
                case 12: return "DELETE_TCB";
                default: return "UNKNOWN(" + state + ")";
            }
        }

        // ─── Minimal JSON parse helpers ───────────────────────────────────────────

        private static string? ParseJsonString(string json, string key)
        {
            // looks for "key":"value"
            string search = "\"" + key + "\":\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static int ParseJsonInt(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return 0;
            int start = idx + search.Length;
            // skip whitespace
            while (start < json.Length && json[start] == ' ') start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (end == start) return 0;
            int.TryParse(json.Substring(start, end - start), out int val);
            return val;
        }

        // ─── JSON output helpers ──────────────────────────────────────────────────

        private static string Err(string msg)
            => "{\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"")
                                    .Replace("\r", "").Replace("\n", " ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }
    }
}
