using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface IConnectionMonitor
{
    IObservable<ConnectionInfo?> ConnectionChanged { get; }
    ConnectionInfo? Current { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}
