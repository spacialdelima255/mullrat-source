using System.Data;
using Microsoft.Data.Sqlite;
using mullvad.Models;

namespace mullvad.Database;

public static class ServerDatabase
{
    private static readonly string DbPath =
        Path.Combine(AppContext.BaseDirectory, "server.db");

    private static string ConnStr => $"Data Source={DbPath}";

    // ─────────────────────────────────────────────────────────────────────────
    //  Initialise
    // ─────────────────────────────────────────────────────────────────────────
    public static void Initialize()
    {
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS clients (
                    client_id    TEXT PRIMARY KEY,
                    computer     TEXT,
                    username     TEXT,
                    ip           TEXT,
                    os           TEXT,
                    arch         TEXT,
                    country      TEXT,
                    connected_at TEXT
                );

                CREATE TABLE IF NOT EXISTS system_info (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    client_id    TEXT,
                    item         TEXT,
                    value        TEXT,
                    collected_at TEXT
                );

                CREATE TABLE IF NOT EXISTS advanced_info (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    client_id    TEXT,
                    category     TEXT,
                    item         TEXT,
                    value        TEXT,
                    collected_at TEXT
                );

                CREATE TABLE IF NOT EXISTS network_info (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    client_id    TEXT,
                    adapter      TEXT,
                    item         TEXT,
                    value        TEXT,
                    collected_at TEXT
                );
            ";
            cmd.ExecuteNonQuery();
        }
        catch { /* never throw from Initialize */ }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Clients
    // ─────────────────────────────────────────────────────────────────────────
    public static void UpsertClient(ClientInfo info)
    {
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO clients
                    (client_id, computer, username, ip, os, arch, country, connected_at)
                VALUES
                    ($id, $computer, $username, $ip, $os, $arch, $country, $connected_at);
            ";
            cmd.Parameters.AddWithValue("$id",           info.Id);
            cmd.Parameters.AddWithValue("$computer",     info.Computer);
            cmd.Parameters.AddWithValue("$username",     info.Username);
            cmd.Parameters.AddWithValue("$ip",           info.DisplayIp);
            cmd.Parameters.AddWithValue("$os",           info.Os);
            cmd.Parameters.AddWithValue("$arch",         info.Architecture);
            cmd.Parameters.AddWithValue("$country",      info.Country);
            cmd.Parameters.AddWithValue("$connected_at", info.ConnectedAt.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public static List<(string clientId, string computer, string username, string os, string collectedAt)>
        GetAllClients()
    {
        var result = new List<(string, string, string, string, string)>();
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT client_id, computer, username, os, connected_at FROM clients ORDER BY connected_at DESC;";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                result.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetString(2),
                            rdr.GetString(3), rdr.GetString(4)));
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  System info
    // ─────────────────────────────────────────────────────────────────────────
    public static void SaveSystemInfo(string clientId, List<(string item, string value)> rows)
    {
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var tx  = con.BeginTransaction();

            using (var del = con.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM system_info WHERE client_id = $id;";
                del.Parameters.AddWithValue("$id", clientId);
                del.ExecuteNonQuery();
            }

            var now = DateTime.UtcNow.ToString("o");
            using var ins = con.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO system_info (client_id, item, value, collected_at) VALUES ($id, $item, $value, $now);";
            var pId    = ins.Parameters.Add("$id",    SqliteType.Text);
            var pItem  = ins.Parameters.Add("$item",  SqliteType.Text);
            var pValue = ins.Parameters.Add("$value", SqliteType.Text);
            var pNow   = ins.Parameters.Add("$now",   SqliteType.Text);

            pId.Value  = clientId;
            pNow.Value = now;

            foreach (var (item, value) in rows)
            {
                pItem.Value  = item;
                pValue.Value = value;
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch { }
    }

    public static List<(string item, string value)> GetSystemInfo(string clientId)
    {
        var result = new List<(string, string)>();
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT item, value FROM system_info WHERE client_id = $id ORDER BY id;";
            cmd.Parameters.AddWithValue("$id", clientId);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                result.Add((rdr.GetString(0), rdr.GetString(1)));
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Advanced info
    // ─────────────────────────────────────────────────────────────────────────
    public static void SaveAdvancedInfo(string clientId, List<(string category, string item, string value)> rows)
    {
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var tx  = con.BeginTransaction();

            using (var del = con.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM advanced_info WHERE client_id = $id;";
                del.Parameters.AddWithValue("$id", clientId);
                del.ExecuteNonQuery();
            }

            var now = DateTime.UtcNow.ToString("o");
            using var ins = con.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO advanced_info (client_id, category, item, value, collected_at) VALUES ($id, $cat, $item, $value, $now);";
            var pId    = ins.Parameters.Add("$id",    SqliteType.Text);
            var pCat   = ins.Parameters.Add("$cat",   SqliteType.Text);
            var pItem  = ins.Parameters.Add("$item",  SqliteType.Text);
            var pValue = ins.Parameters.Add("$value", SqliteType.Text);
            var pNow   = ins.Parameters.Add("$now",   SqliteType.Text);

            pId.Value  = clientId;
            pNow.Value = now;

            foreach (var (cat, item, value) in rows)
            {
                pCat.Value   = cat;
                pItem.Value  = item;
                pValue.Value = value;
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch { }
    }

    public static List<(string category, string item, string value)> GetAdvancedInfo(string clientId)
    {
        var result = new List<(string, string, string)>();
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT category, item, value FROM advanced_info WHERE client_id = $id ORDER BY id;";
            cmd.Parameters.AddWithValue("$id", clientId);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                result.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetString(2)));
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Network info
    // ─────────────────────────────────────────────────────────────────────────
    public static void SaveNetworkInfo(string clientId, List<(string adapter, string item, string value)> rows)
    {
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var tx  = con.BeginTransaction();

            using (var del = con.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM network_info WHERE client_id = $id;";
                del.Parameters.AddWithValue("$id", clientId);
                del.ExecuteNonQuery();
            }

            var now = DateTime.UtcNow.ToString("o");
            using var ins = con.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO network_info (client_id, adapter, item, value, collected_at) VALUES ($id, $adapter, $item, $value, $now);";
            var pId      = ins.Parameters.Add("$id",      SqliteType.Text);
            var pAdapter = ins.Parameters.Add("$adapter", SqliteType.Text);
            var pItem    = ins.Parameters.Add("$item",    SqliteType.Text);
            var pValue   = ins.Parameters.Add("$value",   SqliteType.Text);
            var pNow     = ins.Parameters.Add("$now",     SqliteType.Text);

            pId.Value  = clientId;
            pNow.Value = now;

            foreach (var (adapter, item, value) in rows)
            {
                pAdapter.Value = adapter;
                pItem.Value    = item;
                pValue.Value   = value;
                ins.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch { }
    }

    public static List<(string adapter, string item, string value)> GetNetworkInfo(string clientId)
    {
        var result = new List<(string, string, string)>();
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT adapter, item, value FROM network_info WHERE client_id = $id ORDER BY id;";
            cmd.Parameters.AddWithValue("$id", clientId);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                result.Add((rdr.GetString(0), rdr.GetString(1), rdr.GetString(2)));
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Generic query / table list (for DatabaseViewerForm)
    // ─────────────────────────────────────────────────────────────────────────
    public static DataTable ExecuteQuery(string sql)
    {
        var dt = new DataTable();
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            using var rdr = cmd.ExecuteReader();
            dt.Load(rdr);
        }
        catch (Exception ex)
        {
            dt.Columns.Add("Error");
            dt.Rows.Add(ex.Message);
        }
        return dt;
    }

    public static void ExecuteNonQuery(string sql)
    {
        using var con = new SqliteConnection(ConnStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public static List<string> GetTableNames()
    {
        var result = new List<string>();
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                result.Add(rdr.GetString(0));
        }
        catch { }
        return result;
    }
}
