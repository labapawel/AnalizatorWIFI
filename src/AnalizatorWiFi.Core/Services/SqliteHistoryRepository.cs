using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using Microsoft.Data.Sqlite;

namespace AnalizatorWiFi.Core.Services;

public sealed class SqliteHistoryRepository : IHistoryRepository
{
    private readonly string _dbPath;

    public SqliteHistoryRepository(string dbPath)
    {
        _dbPath = dbPath;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS scan_sessions (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                scanned_at TEXT NOT NULL,
                adapter  TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS scan_results (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL REFERENCES scan_sessions(id),
                bssid      TEXT NOT NULL,
                ssid       TEXT NOT NULL,
                signal_dbm INTEGER NOT NULL,
                channel    INTEGER NOT NULL,
                freq_mhz   REAL NOT NULL,
                band       INTEGER NOT NULL,
                security   INTEGER NOT NULL,
                standard   INTEGER NOT NULL,
                vendor     TEXT NOT NULL,
                scanned_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_results_bssid ON scan_results(bssid, scanned_at);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task SaveScanAsync(ScanResult result, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();

            long sessionId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO scan_sessions(scanned_at, adapter) VALUES($t, $a); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$t", result.ScannedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$a", result.AdapterName);
                sessionId = (long)cmd.ExecuteScalar()!;
            }

            using var insert = conn.CreateCommand();
            insert.CommandText = """
                INSERT INTO scan_results(session_id,bssid,ssid,signal_dbm,channel,freq_mhz,band,security,standard,vendor,scanned_at)
                VALUES($sid,$bssid,$ssid,$sig,$ch,$freq,$band,$sec,$std,$vendor,$at)
                """;

            foreach (var n in result.Networks)
            {
                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("$sid", sessionId);
                insert.Parameters.AddWithValue("$bssid", n.Bssid);
                insert.Parameters.AddWithValue("$ssid", n.Ssid);
                insert.Parameters.AddWithValue("$sig", n.SignalDbm);
                insert.Parameters.AddWithValue("$ch", n.Channel);
                insert.Parameters.AddWithValue("$freq", n.FrequencyMhz);
                insert.Parameters.AddWithValue("$band", (int)n.Band);
                insert.Parameters.AddWithValue("$sec", (int)n.Security);
                insert.Parameters.AddWithValue("$std", (int)n.Standard);
                insert.Parameters.AddWithValue("$vendor", n.Vendor);
                insert.Parameters.AddWithValue("$at", n.ScannedAt.ToString("O"));
                insert.ExecuteNonQuery();
            }

            tx.Commit();
        }, ct);
    }

    public async Task<IReadOnlyList<WifiNetwork>> GetHistoryAsync(string bssid, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT bssid,ssid,signal_dbm,channel,freq_mhz,band,security,standard,vendor,scanned_at
                FROM scan_results
                WHERE bssid=$bssid AND scanned_at >= $from AND scanned_at <= $to
                ORDER BY scanned_at ASC
                """;
            cmd.Parameters.AddWithValue("$bssid", bssid);
            cmd.Parameters.AddWithValue("$from", from.ToString("O"));
            cmd.Parameters.AddWithValue("$to", to.ToString("O"));

            var list = new List<WifiNetwork>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new WifiNetwork
                {
                    Bssid = reader.GetString(0),
                    Ssid = reader.GetString(1),
                    SignalDbm = reader.GetInt32(2),
                    Channel = reader.GetInt32(3),
                    FrequencyMhz = reader.GetDouble(4),
                    Band = (WifiBand)reader.GetInt32(5),
                    Security = (WifiSecurity)reader.GetInt32(6),
                    Standard = (WifiStandard)reader.GetInt32(7),
                    Vendor = reader.GetString(8),
                    ScannedAt = DateTimeOffset.Parse(reader.GetString(9))
                });
            }
            return (IReadOnlyList<WifiNetwork>)list;
        }, ct);
    }

    public async Task<IReadOnlyList<string>> GetTrackedBssidsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT bssid FROM scan_results ORDER BY bssid";
            var list = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetString(0));
            return (IReadOnlyList<string>)list;
        }, ct);
    }

    public async Task PurgeOldEntriesAsync(int maxAgeDays, CancellationToken ct = default)
    {
        if (maxAgeDays <= 0) return;
        await Task.Run(() =>
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            string cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays).ToString("O");
            cmd.CommandText = "DELETE FROM scan_results WHERE scanned_at < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff);
            cmd.ExecuteNonQuery();
            cmd.CommandText = "DELETE FROM scan_sessions WHERE id NOT IN (SELECT DISTINCT session_id FROM scan_results)";
            cmd.Parameters.Clear();
            cmd.ExecuteNonQuery();
        }, ct);
    }

    private SqliteConnection Open()
    {
        string dir = Path.GetDirectoryName(_dbPath)!;
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }
}
