using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface ITcpConnectionReader
{
    Task<IReadOnlyList<TcpConnectionEntry>> GetConnectionsAsync(CancellationToken ct = default);
}
