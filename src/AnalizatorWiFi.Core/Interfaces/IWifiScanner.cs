using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface IWifiScanner
{
    Task<ScanResult> ScanAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAdaptersAsync(CancellationToken ct = default);
    void SetAdapter(string adapterName);
}
