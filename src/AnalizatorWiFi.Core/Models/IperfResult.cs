namespace AnalizatorWiFi.Core.Models;

public sealed class IperfResult
{
    public string ServerAddress { get; init; } = string.Empty;
    public double DownloadMbps { get; init; }
    public double UploadMbps { get; init; }
    public double JitterMs { get; init; }
    public double LostPercent { get; init; }
    public int DurationSeconds { get; init; }
    public DateTimeOffset TestedAt { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
