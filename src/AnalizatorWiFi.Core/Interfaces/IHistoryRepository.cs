using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface IHistoryRepository
{
    Task SaveScanAsync(ScanResult result, CancellationToken ct = default);
    Task<IReadOnlyList<WifiNetwork>> GetHistoryAsync(string bssid, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetTrackedBssidsAsync(CancellationToken ct = default);
    Task PurgeOldEntriesAsync(int maxAgeDays, CancellationToken ct = default);
}
