using System.Reactive.Subjects;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Services;

public sealed class ConnectionMonitorService : IConnectionMonitor, IDisposable
{
    private readonly IWifiConnector _connector;
    private readonly BehaviorSubject<ConnectionInfo?> _subject = new(null);
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public IObservable<ConnectionInfo?> ConnectionChanged => _subject;
    public ConnectionInfo? Current => _subject.Value;

    public ConnectionMonitorService(IWifiConnector connector)
    {
        _connector = connector;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await StopAsync();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _monitorTask = MonitorLoop(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts != null) await _cts.CancelAsync();
        if (_monitorTask != null)
            await _monitorTask.ConfigureAwait(false);
        _cts?.Dispose();
        _cts = null;
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var info = await _connector.GetCurrentConnectionAsync(ct);
                if (!Equals(_subject.Value?.Bssid, info?.Bssid))
                    _subject.OnNext(info);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore transient errors */ }

            await Task.Delay(3000, ct).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _subject.Dispose();
    }
}
