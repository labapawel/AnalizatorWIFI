using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface IWifiConnector
{
    Task<bool> ConnectAsync(string ssid, string? password, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<ConnectionInfo?> GetCurrentConnectionAsync(CancellationToken ct = default);
    Task<bool> IsConnectedAsync(CancellationToken ct = default);
}
