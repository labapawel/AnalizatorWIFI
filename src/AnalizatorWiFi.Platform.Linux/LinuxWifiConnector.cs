using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.Core.Services;

namespace AnalizatorWiFi.Platform.Linux;

public sealed class LinuxWifiConnector : IWifiConnector
{
    private string _selectedAdapter = string.Empty;

    public void SetAdapter(string adapterName) => _selectedAdapter = adapterName;

    public async Task<bool> ConnectAsync(string ssid, string? password, CancellationToken ct = default)
    {
        string ifnamePart = string.IsNullOrEmpty(_selectedAdapter)
            ? string.Empty
            : $" ifname {_selectedAdapter}";

        string args = string.IsNullOrEmpty(password)
            ? $"dev wifi connect \"{EscapeShell(ssid)}\"{ifnamePart}"
            : $"dev wifi connect \"{EscapeShell(ssid)}\" password \"{EscapeShell(password)}\"{ifnamePart}";

        var (_, error, exitCode) = await ShellRunner.RunAsync("nmcli", args, ct);
        return exitCode == 0;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        string adapter = !string.IsNullOrEmpty(_selectedAdapter)
            ? _selectedAdapter
            : await GetWifiAdapterAsync(ct) ?? string.Empty;

        if (!string.IsNullOrEmpty(adapter))
            await ShellRunner.RunAsync("nmcli", $"dev disconnect {adapter}", ct);
    }

    public async Task<ConnectionInfo?> GetCurrentConnectionAsync(CancellationToken ct = default)
    {
        var (output, _, exitCode) = await ShellRunner.RunAsync("nmcli",
            "-t -f DEVICE,TYPE,STATE,CONNECTION dev", ct);

        if (exitCode != 0) return null;

        string? connectedDevice = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split(':'))
            .Where(p => p.Length >= 3 && p[1] == "wifi" && p[2] == "connected" &&
                        (string.IsNullOrEmpty(_selectedAdapter) || p[0] == _selectedAdapter))
            .Select(p => p[0])
            .FirstOrDefault();

        if (connectedDevice == null) return null;

        var (detailOutput, _, _) = await ShellRunner.RunAsync("nmcli",
            $"-t -f GENERAL.CONNECTION,GENERAL.HWADDR,IP4.ADDRESS,IP4.GATEWAY,IP4.DNS,802-11-wireless.ssid dev show {connectedDevice}", ct);

        return ParseConnectionInfo(detailOutput, connectedDevice);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        var (output, _, exitCode) = await ShellRunner.RunAsync("nmcli",
            "-t -f TYPE,STATE dev", ct);
        if (exitCode != 0) return false;
        return output.Split('\n').Any(l => l.Contains("wifi:connected"));
    }

    private static ConnectionInfo? ParseConnectionInfo(string output, string device)
    {
        var fields = ParseFields(output);

        string ssid = fields.GetValueOrDefault("GENERAL.CONNECTION", string.Empty);
        string mac = fields.GetValueOrDefault("GENERAL.HWADDR", string.Empty);
        string ipWithPrefix = fields.GetValueOrDefault("IP4.ADDRESS[1]", string.Empty);
        string gateway = fields.GetValueOrDefault("IP4.GATEWAY", string.Empty);

        string ip = ipWithPrefix.Contains('/') ? ipWithPrefix.Split('/')[0] : ipWithPrefix;
        string mask = ipWithPrefix.Contains('/') ? PrefixToMask(int.Parse(ipWithPrefix.Split('/')[1])) : string.Empty;

        var dns = fields.Where(kv => kv.Key.StartsWith("IP4.DNS"))
            .Select(kv => kv.Value)
            .ToList();

        return new ConnectionInfo
        {
            Ssid = ssid,
            Bssid = mac,
            IpAddress = ip,
            SubnetMask = mask,
            Gateway = gateway,
            DnsServers = dns,
            NetworkInterface = device,
            CapturedAt = DateTimeOffset.Now
        };
    }

    private static Dictionary<string, string> ParseFields(string output)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            string key = line[..colon].Trim();
            string val = line[(colon + 1)..].Trim();
            dict[key] = val;
        }
        return dict;
    }

    private static string PrefixToMask(int prefix)
    {
        uint mask = prefix == 0 ? 0 : ~(uint.MaxValue >> prefix);
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    private static async Task<string?> GetWifiAdapterAsync(CancellationToken ct)
    {
        var (output, _, _) = await ShellRunner.RunAsync("nmcli", "-t -f DEVICE,TYPE,STATE dev", ct);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split(':'))
            .Where(p => p.Length >= 2 && p[1] == "wifi")
            .Select(p => p[0])
            .FirstOrDefault();
    }

    private static string EscapeShell(string value) => value.Replace("\"", "\\\"");
}
