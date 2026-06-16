using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.Core.Services;
using AnalizatorWiFi.Platform.Windows.WlanApi;

namespace AnalizatorWiFi.Platform.Windows;

public sealed partial class WindowsWifiScanner : IWifiScanner
{
    private readonly IOuiLookup _ouiLookup;
    private string _selectedAdapter = string.Empty;

    // Regexes for locale-independent netsh parsing
    [GeneratedRegex(@"^SSID\s+\d+\s*:\s*(.*)$",          RegexOptions.IgnoreCase)] private static partial Regex SsidLine();
    [GeneratedRegex(@"^BSSID\s+\d+\s*:\s*([0-9a-f:]{17})", RegexOptions.IgnoreCase)] private static partial Regex BssidLine();
    [GeneratedRegex(@"Signal\s*:\s*(\d+)%",               RegexOptions.IgnoreCase)] private static partial Regex SignalLine();
    [GeneratedRegex(@"Radio type\s*:\s*(802\.\S+)",        RegexOptions.IgnoreCase)] private static partial Regex RadioLine();
    [GeneratedRegex(@"Band\s*:\s*(.+GHz)",                 RegexOptions.IgnoreCase)] private static partial Regex BandLine();
    [GeneratedRegex(@"Channel\s*:\s*(\d+)",                RegexOptions.IgnoreCase)] private static partial Regex ChannelLine();
    [GeneratedRegex(@"Authentication\s*:\s*(.+)",          RegexOptions.IgnoreCase)] private static partial Regex AuthLine();
    [GeneratedRegex(@"Encryption\s*:\s*(.+)",              RegexOptions.IgnoreCase)] private static partial Regex EncLine();

    public WindowsWifiScanner(IOuiLookup ouiLookup) => _ouiLookup = ouiLookup;

    public void SetAdapter(string adapterName) => _selectedAdapter = adapterName;

    public async Task<IReadOnlyList<string>> GetAdaptersAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var h = new WlanHandle();
            return (IReadOnlyList<string>)GetInterfaces(h).Select(i => i.strInterfaceDescription).ToList();
        }, ct);
    }

    public async Task<ScanResult> ScanAsync(CancellationToken ct = default)
    {
        try
        {
            string output = await RunNetshAsync(ct);
            var networks = ParseNetshOutput(output);
            return new ScanResult
            {
                Networks    = networks,
                AdapterName = _selectedAdapter,
                ScannedAt   = DateTimeOffset.Now,
                IsSuccess   = true
            };
        }
        catch (Exception ex)
        {
            return new ScanResult { IsSuccess = false, ErrorMessage = ex.Message, ScannedAt = DateTimeOffset.Now };
        }
    }

    // -----------------------------------------------------------------------
    // netsh runner
    // -----------------------------------------------------------------------

    private async Task<string> RunNetshAsync(CancellationToken ct)
    {
        string args = string.IsNullOrEmpty(_selectedAdapter)
            ? "wlan show networks mode=bssid"
            : $"wlan show networks interface=\"{_selectedAdapter}\" mode=bssid";

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo("netsh", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };
        proc.Start();
        string stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return stdout;
    }

    // -----------------------------------------------------------------------
    // Parser — state machine over lines
    // -----------------------------------------------------------------------

    private List<WifiNetwork> ParseNetshOutput(string output)
    {
        var result = new List<WifiNetwork>();

        // Per-SSID context
        string currentSsid = string.Empty;
        string currentAuth = string.Empty;
        string currentEnc  = string.Empty;

        // Per-BSSID context
        string currentBssid   = string.Empty;
        int    currentSignal  = 0;
        string currentRadio   = string.Empty;
        string currentBand    = string.Empty;
        int    currentChannel = 0;
        bool   inBssid        = false;

        void FlushBssid()
        {
            if (!inBssid || string.IsNullOrEmpty(currentBssid)) return;
            double freqMhz = ChannelCalculator.ChannelToFrequency(currentBand, currentChannel);
            int    dbm     = (int)(currentSignal / 2.0 - 100.0);

            result.Add(new WifiNetwork
            {
                Ssid            = currentSsid,
                Bssid           = currentBssid,
                SignalDbm       = dbm,
                FrequencyMhz    = freqMhz,
                Channel         = currentChannel > 0 ? currentChannel : ChannelCalculator.FrequencyToChannel(freqMhz),
                Band            = ChannelCalculator.GetBand(freqMhz),
                ChannelWidthMhz = 20,
                Standard        = ParseRadioType(currentRadio),
                Security        = ParseSecurity(currentAuth, currentEnc),
                IsHidden        = string.IsNullOrEmpty(currentSsid),
                Vendor          = _ouiLookup.GetVendor(currentBssid),
                ScannedAt       = DateTimeOffset.Now
            });
            inBssid = false;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();

            var ssidMatch = SsidLine().Match(line);
            if (ssidMatch.Success)
            {
                FlushBssid();
                currentSsid    = ssidMatch.Groups[1].Value.Trim();
                currentAuth    = string.Empty;
                currentEnc     = string.Empty;
                continue;
            }

            var bssidMatch = BssidLine().Match(line);
            if (bssidMatch.Success)
            {
                FlushBssid();
                currentBssid   = bssidMatch.Groups[1].Value.Trim().ToUpperInvariant();
                currentSignal  = 0;
                currentRadio   = string.Empty;
                currentBand    = string.Empty;
                currentChannel = 0;
                inBssid        = true;
                continue;
            }

            var authMatch = AuthLine().Match(line);
            if (authMatch.Success)  { currentAuth = authMatch.Groups[1].Value.Trim(); continue; }

            var encMatch = EncLine().Match(line);
            if (encMatch.Success)   { currentEnc = encMatch.Groups[1].Value.Trim(); continue; }

            if (!inBssid) continue;

            var signalMatch = SignalLine().Match(line);
            if (signalMatch.Success) { currentSignal = int.Parse(signalMatch.Groups[1].Value); continue; }

            var radioMatch = RadioLine().Match(line);
            if (radioMatch.Success)  { currentRadio = radioMatch.Groups[1].Value.Trim(); continue; }

            var bandMatch = BandLine().Match(line);
            if (bandMatch.Success)   { currentBand = bandMatch.Groups[1].Value.Trim(); continue; }

            var chMatch = ChannelLine().Match(line);
            if (chMatch.Success)     { currentChannel = int.Parse(chMatch.Groups[1].Value); continue; }
        }

        FlushBssid();
        return [.. result.OrderByDescending(n => n.SignalDbm)];
    }

    // -----------------------------------------------------------------------
    // Mappings
    // -----------------------------------------------------------------------

    private static WifiStandard ParseRadioType(string radio)
    {
        if (radio.Contains("802.11be", StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11be;
        if (radio.Contains("802.11ax", StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11ax;
        if (radio.Contains("802.11ac", StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11ac;
        if (radio.Contains("802.11n",  StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11n;
        if (radio.Contains("802.11g",  StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11g;
        if (radio.Contains("802.11a",  StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11a;
        if (radio.Contains("802.11b",  StringComparison.OrdinalIgnoreCase)) return WifiStandard.Dot11b;
        return WifiStandard.Unknown;
    }

    private static WifiSecurity ParseSecurity(string auth, string enc)
    {
        if (auth.Contains("WPA3",       StringComparison.OrdinalIgnoreCase)) return WifiSecurity.WPA3;
        if (auth.Contains("WPA2",       StringComparison.OrdinalIgnoreCase)) return WifiSecurity.WPA2;
        if (auth.Contains("WPA",        StringComparison.OrdinalIgnoreCase)) return WifiSecurity.WPA;
        if (auth.Contains("WEP",        StringComparison.OrdinalIgnoreCase) ||
            enc.Contains("WEP",         StringComparison.OrdinalIgnoreCase)) return WifiSecurity.WEP;
        if (auth.Contains("Open",       StringComparison.OrdinalIgnoreCase) ||
            auth.Contains("None",       StringComparison.OrdinalIgnoreCase)) return WifiSecurity.Open;
        return WifiSecurity.Open;
    }

    // -----------------------------------------------------------------------
    // WlanApi interface enumeration (still needed for adapter list)
    // -----------------------------------------------------------------------

    private static List<WlanInterfaceInfo> GetInterfaces(WlanHandle handle)
    {
        uint r = NativeMethods.WlanEnumInterfaces(handle.Handle, IntPtr.Zero, out IntPtr ptr);
        if (r != NativeMethods.ERROR_SUCCESS) return [];
        try
        {
            uint   count = (uint)Marshal.ReadInt32(ptr);
            IntPtr ip    = ptr + 8;
            int    sz    = Marshal.SizeOf<WlanInterfaceInfo>();
            var    list  = new List<WlanInterfaceInfo>((int)count);
            for (int i = 0; i < count; i++) { list.Add(Marshal.PtrToStructure<WlanInterfaceInfo>(ip)); ip += sz; }
            return list;
        }
        finally { NativeMethods.WlanFreeMemory(ptr); }
    }
}
