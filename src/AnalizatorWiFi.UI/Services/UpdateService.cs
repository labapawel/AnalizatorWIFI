using Velopack;
using Velopack.Sources;

namespace AnalizatorWiFi.UI.Services;

public class UpdateService
{
    private const string UpdateUrl = "https://analizatorwifi.ebtech.pl/soft";

    private readonly UpdateManager _manager;
    private UpdateInfo? _pendingUpdate;

    public UpdateService()
    {
        _manager = new UpdateManager(new SimpleWebSource(UpdateUrl));
    }

    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? "dev";

    public bool HasPendingUpdate => _pendingUpdate != null;
    public string? PendingVersion => _pendingUpdate?.TargetFullRelease.Version.ToString();

    public async Task<bool> CheckForUpdatesAsync()
    {
        _pendingUpdate = await _manager.CheckForUpdatesAsync();
        return _pendingUpdate != null;
    }

    public async Task DownloadUpdateAsync(Action<int>? onProgress = null)
    {
        if (_pendingUpdate == null)
            throw new InvalidOperationException("No update to download.");
        await _manager.DownloadUpdatesAsync(_pendingUpdate, onProgress);
    }

    public void ApplyAndRestart()
    {
        if (_pendingUpdate == null)
            throw new InvalidOperationException("No update to apply.");
        _manager.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
