using System.Text.RegularExpressions;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.Core.Services;

namespace AnalizatorWiFi.Platform.Linux;

public sealed class LinuxWifiScanner : IWifiScanner
{
    private readonly IOuiLookup _ouiLookup;
    private string _selectedAdapter = string.Empty;

    public LinuxWifiScanner(IOuiLookup ouiLookup)
    {
        _ouiLookup = ouiLookup;
    }

    public void SetAdapter(string adapterName) => _selectedAdapter = adapterName;

    public async Task<IReadOnlyList<string>> GetAdaptersAsync(CancellationToken ct = default)
    {
        var (output, _, _) = await ShellRunner.RunAsync("nmcli", "-t -f DEVICE,TYPE device", ct);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':'))
            .Where(parts => parts.Length >= 2 && parts[1].Trim() == "wifi")
            .Select(parts => parts[0].Trim())
            .ToList();
    }

    public async Task<ScanResult> ScanAsync(CancellationToken ct = default)
    {
        try
        {
            string adapter = _selectedAdapter;
            if (string.IsNullOrEmpty(adapter))
            {
                var adapters = await GetAdaptersAsync(ct);
                adapter = adapters.FirstOrDefault() ?? string.Empty;
            }

            // Trigger rescan then read results
            await ShellRunner.RunAsync("nmcli", $"dev wifi rescan ifname {adapter}", ct);
            await Task.Delay(1500, ct); // wait for scan results

            var (output, error, exitCode) = await ShellRunner.RunAsync("nmcli",
                $"-t -f BSSID,SSID,CHAN,FREQ,SIGNAL,SECURITY,MODE dev wifi list ifname {adapter}", ct);

            if (exitCode != 0)
                return new ScanResult { IsSuccess = false, ErrorMessage = error, ScannedAt = DateTimeOffset.Now };

            var networks = ParseNmcliOutput(output);

            return new ScanResult
            {
                Networks = networks,
                AdapterName = adapter,
                ScannedAt = DateTimeOffset.Now,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            return new ScanResult { IsSuccess = false, ErrorMessage = ex.Message, ScannedAt = DateTimeOffset.Now };
        }
    }

    private List<WifiNetwork> ParseNmcliOutput(string output)
    {
        var networks = new List<WifiNetwork>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // nmcli -t uses colons, but BSSIDs also contain colons — nmcli escapes them as \:
            // Format: BSSID:SSID:CHAN:FREQ:SIGNAL:SECURITY:MODE
            var parts = SplitNmcliLine(line);
            if (parts.Length < 7) continue;

            string bssid = parts[0].Replace("\\:", ":");
            string ssid = parts[1].Replace("\\:", ":");
            string freqStr = parts[3]; // e.g. "2437 MHz"
            double freqMhz = ParseFrequency(freqStr);
            int signal = int.TryParse(parts[4], out int sig) ? sig : 0;
            int signalDbm = SignalPercentToDbm(signal);
            string security = parts[5];
            string mode = parts[6];

            if (!int.TryParse(parts[2], out int channel))
                channel = ChannelCalculator.FrequencyToChannel(freqMhz);

            networks.Add(new WifiNetwork
            {
                Ssid = ssid,
                Bssid = bssid,
                SignalDbm = signalDbm,
                Channel = channel,
                FrequencyMhz = freqMhz,
                Band = ChannelCalculator.GetBand(freqMhz),
                ChannelWidthMhz = 20,
                Security = ParseSecurity(security),
                Standard = GuessStandard(freqMhz, mode),
                IsHidden = string.IsNullOrWhiteSpace(ssid),
                Vendor = _ouiLookup.GetVendor(bssid),
                ScannedAt = DateTimeOffset.Now
            });
        }

        return networks;
    }

    // nmcli -t escapes colons in values with \:, so split on unescaped colons
    private static string[] SplitNmcliLine(string line)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\' && i + 1 < line.Length && line[i + 1] == ':')
            {
                current.Append("\\:");
                i++;
            }
            else if (line[i] == ':')
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(line[i]);
            }
        }
        parts.Add(current.ToString());
        return [.. parts];
    }

    private static double ParseFrequency(string freqStr)
    {
        var match = Regex.Match(freqStr, @"(\d+(?:\.\d+)?)");
        if (!match.Success) return 0;
        double val = double.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture);
        // nmcli returns MHz directly (e.g. "2437 MHz")
        return val;
    }

    private static int SignalPercentToDbm(int percent) => (percent / 2) - 100;

    private static WifiSecurity ParseSecurity(string security)
    {
        if (string.IsNullOrWhiteSpace(security) || security == "--") return WifiSecurity.Open;
        var result = WifiSecurity.Unknown;
        if (security.Contains("WPA3")) result |= WifiSecurity.WPA3;
        if (security.Contains("WPA2")) result |= WifiSecurity.WPA2;
        if (security.Contains("WPA1") || (security.Contains("WPA") && !security.Contains("WPA2") && !security.Contains("WPA3")))
            result |= WifiSecurity.WPA;
        if (security.Contains("WEP")) result |= WifiSecurity.WEP;
        if (security.Contains("802.1X")) result |= WifiSecurity.Enterprise;
        return result == WifiSecurity.Unknown ? WifiSecurity.Open : result;
    }

    private static WifiStandard GuessStandard(double freqMhz, string mode)
    {
        // nmcli MODE field: "Infra", "AP", etc. — standard not directly available
        // Guess from frequency band as fallback
        return ChannelCalculator.GetBand(freqMhz) switch
        {
            WifiBand.GHz6 => WifiStandard.Dot11ax,
            WifiBand.GHz5 => WifiStandard.Dot11ac,
            WifiBand.GHz2_4 => WifiStandard.Dot11n,
            _ => WifiStandard.Unknown
        };
    }
}
