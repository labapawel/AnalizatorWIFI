namespace AnalizatorWiFi.Core.Models;

public sealed record ConnectionInfo
{
    public string Ssid { get; init; } = string.Empty;
    public string Bssid { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string SubnetMask { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public IReadOnlyList<string> DnsServers { get; init; } = [];
    public string NetworkInterface { get; init; } = string.Empty;
    public int LinkSpeedMbps { get; init; }
    public int SignalDbm { get; init; }
    public int Channel { get; init; }
    public WifiBand Band { get; init; }
    public WifiStandard Standard { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
}
