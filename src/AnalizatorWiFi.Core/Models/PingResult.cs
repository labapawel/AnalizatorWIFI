namespace AnalizatorWiFi.Core.Models;

public sealed class PingResult
{
    public string Host { get; init; } = string.Empty;
    public double AverageMs { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double LostPercent { get; init; }
    public int PacketsSent { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
