using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.Platform.Windows.WlanApi;

namespace AnalizatorWiFi.Platform.Windows;

public sealed partial class WindowsWifiConnector : IWifiConnector
{
    // netsh wlan show interfaces — SSID/BSSID labels are never localized
    [GeneratedRegex(@"^\s+SSID\s*:\s*(.+)$",                                                    RegexOptions.IgnoreCase)] private static partial Regex SsidLine();
    [GeneratedRegex(@"^\s+BSSID\s*:\s*([0-9a-f]{2}[:\-][0-9a-f]{2}[:\-][0-9a-f]{2}[:\-][0-9a-f]{2}[:\-][0-9a-f]{2}[:\-][0-9a-f]{2})", RegexOptions.IgnoreCase)] private static partial Regex BssidLine();
    [GeneratedRegex(@"^\s+(?:State|Stan)\s*:\s*(.+)$",                                          RegexOptions.IgnoreCase)] private static partial Regex StateLine();
    [GeneratedRegex(@"^\s+(?:Signal|Sygna[łl])\s*:\s*(\d+)%",                                  RegexOptions.IgnoreCase)] private static partial Regex SignalLine();
    [GeneratedRegex(@"^\s+(?:Radio type|Typ radia)\s*:\s*(802\.\S+)",                           RegexOptions.IgnoreCase)] private static partial Regex RadioLine();
    [GeneratedRegex(@"^\s+(?:Channel|Kana[łl])\s*:\s*(\d+)",                                   RegexOptions.IgnoreCase)] private static partial Regex ChannelLine();
    [GeneratedRegex(@"^\s+.{5,40}\(Mb[/p]s\)\s*:\s*([\d.,]+)",                                 RegexOptions.IgnoreCase)] private static partial Regex RxRateLine();

    public async Task<bool> ConnectAsync(string ssid, string? password, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var handle = new WlanHandle();
                var iface = GetFirstInterface(handle);
                if (iface.InterfaceGuid == Guid.Empty) return false;

                string profileXml = BuildProfileXml(ssid, password);
                var guid = iface.InterfaceGuid;
                NativeMethods.WlanSetProfile(handle.Handle, ref guid, 0, profileXml, null, true, IntPtr.Zero, out _);

                var connParams = new WlanConnectionParameters
                {
                    wlanConnectionMode = WlanConnectionMode.Profile,
                    strProfile = ssid,
                    dot11BssType = Dot11BssType.Infrastructure,
                    dwFlags = 0
                };

                uint result = NativeMethods.WlanConnect(handle.Handle, ref guid, ref connParams, IntPtr.Zero);
                return result == NativeMethods.ERROR_SUCCESS;
            }
            catch { return false; }
        }, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var handle = new WlanHandle();
            var iface = GetFirstInterface(handle);
            if (iface.InterfaceGuid == Guid.Empty) return;
            var guid = iface.InterfaceGuid;
            NativeMethods.WlanDisconnect(handle.Handle, ref guid, IntPtr.Zero);
        }, ct);
    }

    public async Task<ConnectionInfo?> GetCurrentConnectionAsync(CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi)!;
                string output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                return ParseInterfacesOutput(output);
            }
            catch { return null; }
        }, ct);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var handle = new WlanHandle();
            var iface = GetFirstInterface(handle);
            return iface.isState == WlanInterfaceState.Connected;
        }, ct);
    }

    private ConnectionInfo? ParseInterfacesOutput(string output)
    {
        string? ssid = null, bssid = null, radioType = null;
        int signalPct = 0, channel = 0;
        double rxRate = 0;
        bool rxRateSet = false;
        bool stateConnected = false;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            var m = StateLine().Match(line);
            if (m.Success)
            {
                string st = m.Groups[1].Value.Trim();
                stateConnected = st.Contains("connect", StringComparison.OrdinalIgnoreCase)
                              || st.StartsWith("połączon", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            // BSSID before SSID — the more specific label first
            m = BssidLine().Match(line);
            if (m.Success)
            {
                // normalise separator to colon
                bssid = m.Groups[1].Value.Trim().Replace('-', ':').ToUpperInvariant();
                continue;
            }

            m = SsidLine().Match(line);
            if (m.Success) { ssid ??= m.Groups[1].Value.Trim(); continue; }

            m = SignalLine().Match(line);
            if (m.Success) { signalPct = int.Parse(m.Groups[1].Value); continue; }

            m = RadioLine().Match(line);
            if (m.Success) { radioType = m.Groups[1].Value.Trim(); continue; }

            m = ChannelLine().Match(line);
            if (m.Success) { channel = int.Parse(m.Groups[1].Value); continue; }

            if (!rxRateSet)
            {
                m = RxRateLine().Match(line);
                if (m.Success)
                {
                    rxRate = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                    rxRateSet = true;
                }
            }
        }

        // Connected if state says so OR if netsh returned an SSID (state label may differ on some locales)
        bool connected = stateConnected || !string.IsNullOrEmpty(ssid);
        if (!connected) return null;

        int signalDbm = (int)(signalPct / 2.0 - 100);
        var (ip, mask, gw, dns) = GetNetworkInfo();

        return new ConnectionInfo
        {
            Ssid = ssid ?? string.Empty,
            Bssid = bssid,
            IpAddress = ip,
            SubnetMask = mask,
            Gateway = gw,
            DnsServers = dns,
            LinkSpeedMbps = (int)rxRate,
            SignalDbm = signalDbm,
            Channel = channel,
            Standard = RadioTypeToStandard(radioType),
            CapturedAt = DateTimeOffset.Now
        };
    }

    private static (string ip, string mask, string gw, IReadOnlyList<string> dns) GetNetworkInfo()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) continue;
            if (nic.OperationalStatus != OperationalStatus.Up) continue;

            var props = nic.GetIPProperties();
            var ipInfo = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipInfo == null) continue;

            string ip   = ipInfo.Address.ToString();
            string mask = ipInfo.IPv4Mask?.ToString() ?? string.Empty;
            string gw   = props.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? string.Empty;
            var dns     = props.DnsAddresses.Select(d => d.ToString()).ToList();

            return (ip, mask, gw, dns);
        }
        return (string.Empty, string.Empty, string.Empty, []);
    }

    private static WlanInterfaceInfo GetFirstInterface(WlanHandle handle)
    {
        uint result = NativeMethods.WlanEnumInterfaces(handle.Handle, IntPtr.Zero, out IntPtr listPtr);
        if (result != NativeMethods.ERROR_SUCCESS) return default;
        try
        {
            uint count = (uint)Marshal.ReadInt32(listPtr);
            if (count == 0) return default;
            return Marshal.PtrToStructure<WlanInterfaceInfo>(listPtr + 8);
        }
        finally { NativeMethods.WlanFreeMemory(listPtr); }
    }

    private static WifiStandard RadioTypeToStandard(string? radio) => radio switch
    {
        string r when r.Contains("802.11be", StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11be,
        string r when r.Contains("802.11ax", StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11ax,
        string r when r.Contains("802.11ac", StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11ac,
        string r when r.Contains("802.11n",  StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11n,
        string r when r.Contains("802.11g",  StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11g,
        string r when r.Contains("802.11a",  StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11a,
        string r when r.Contains("802.11b",  StringComparison.OrdinalIgnoreCase) => WifiStandard.Dot11b,
        _ => WifiStandard.Unknown
    };

    private static string BuildProfileXml(string ssid, string? password)
    {
        string auth = string.IsNullOrEmpty(password) ? "open" : "WPA2PSK";
        string cipher = string.IsNullOrEmpty(password) ? "none" : "AES";
        string keySection = string.IsNullOrEmpty(password) ? string.Empty : $"""
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{SecurityElement(password)}</keyMaterial>
            </sharedKey>
            """;

        return $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
                <name>{SecurityElement(ssid)}</name>
                <SSIDConfig><SSID><name>{SecurityElement(ssid)}</name></SSID></SSIDConfig>
                <connectionType>ESS</connectionType>
                <connectionMode>manual</connectionMode>
                <MSM>
                    <security>
                        <authEncryption>
                            <authentication>{auth}</authentication>
                            <encryption>{cipher}</encryption>
                            <useOneX>false</useOneX>
                        </authEncryption>
                        {keySection}
                    </security>
                </MSM>
            </WLANProfile>
            """;
    }

    private static string SecurityElement(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
