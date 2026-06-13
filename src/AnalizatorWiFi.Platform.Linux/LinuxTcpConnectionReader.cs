using System.Globalization;
using System.Net;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Platform.Linux;

public sealed class LinuxTcpConnectionReader : ITcpConnectionReader
{
    private static readonly Dictionary<string, string> StateMap = new()
    {
        ["01"] = "ESTABLISHED", ["02"] = "SYN_SENT",  ["03"] = "SYN_RCVD",
        ["04"] = "FIN_WAIT1",   ["05"] = "FIN_WAIT2", ["06"] = "TIME_WAIT",
        ["07"] = "CLOSED",      ["08"] = "CLOSE_WAIT", ["09"] = "LAST_ACK",
        ["0A"] = "LISTEN",      ["0B"] = "CLOSING",
    };

    public async Task<IReadOnlyList<TcpConnectionEntry>> GetConnectionsAsync(CancellationToken ct = default)
    {
        var results = new List<TcpConnectionEntry>();
        await ParseFileAsync("/proc/net/tcp", "TCP", results, ct);
        return results;
    }

    private static async Task ParseFileAsync(
        string path, string protocol, List<TcpConnectionEntry> results, CancellationToken ct)
    {
        if (!File.Exists(path)) return;

        var lines = await File.ReadAllLinesAsync(path, ct);
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            // local_address and rem_address are "XXXXXXXX:XXXX"
            var local = parts[1].Split(':');
            var remote = parts[2].Split(':');
            if (local.Length < 2 || remote.Length < 2) continue;

            string stateKey = parts[3].ToUpperInvariant();
            StateMap.TryGetValue(stateKey, out var state);

            results.Add(new TcpConnectionEntry
            {
                Protocol = protocol,
                LocalAddress = ParseHexIp(local[0]),
                LocalPort = ParseHexPort(local[1]),
                RemoteAddress = ParseHexIp(remote[0]),
                RemotePort = ParseHexPort(remote[1]),
                State = state ?? stateKey,
            });
        }
    }

    // /proc/net/tcp stores IP in little-endian hex: "0101A8C0" → 192.168.1.1
    private static string ParseHexIp(string hex)
    {
        if (hex.Length != 8) return "0.0.0.0";
        if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out uint addr)) return "0.0.0.0";
        var b = new byte[4];
        b[0] = (byte)(addr & 0xFF);
        b[1] = (byte)((addr >> 8) & 0xFF);
        b[2] = (byte)((addr >> 16) & 0xFF);
        b[3] = (byte)((addr >> 24) & 0xFF);
        return new IPAddress(b).ToString();
    }

    // Port is stored as big-endian hex: "0050" → 80
    private static int ParseHexPort(string hex)
        => int.TryParse(hex, NumberStyles.HexNumber, null, out int p) ? p : 0;
}
