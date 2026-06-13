namespace AnalizatorWiFi.Core.Models;

public sealed class ScanResult
{
    public IReadOnlyList<WifiNetwork> Networks { get; init; } = [];
    public DateTimeOffset ScannedAt { get; init; }
    public string AdapterName { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
