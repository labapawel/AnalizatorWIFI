namespace AnalizatorWiFi.Core.Models;

public sealed class WifiNetwork
{
    public string Ssid { get; init; } = string.Empty;
    public string Bssid { get; init; } = string.Empty;
    public int SignalDbm { get; init; }
    public int Channel { get; init; }
    public double FrequencyMhz { get; init; }
    public WifiBand Band { get; init; }
    public int ChannelWidthMhz { get; init; }
    public WifiSecurity Security { get; init; }
    public WifiStandard Standard { get; init; }
    public bool IsHidden { get; init; }
    public string Vendor { get; init; } = string.Empty;
    public DateTimeOffset ScannedAt { get; init; }

    public int SignalPercent => Math.Clamp(2 * (SignalDbm + 100), 0, 100);

    // Estimated distance using free-space path loss (FSPL) approximation
    public double EstimatedDistanceMeters
    {
        get
        {
            if (FrequencyMhz <= 0) return 0;
            double exp = (27.55 - (20 * Math.Log10(FrequencyMhz)) - SignalDbm) / 20.0;
            return Math.Pow(10, exp);
        }
    }
}
