using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Services;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class AnalyzerViewModel : ViewModelBase
{
    private readonly ITcpConnectionReader _reader;
    private readonly GeoLocationService _geoService;

    // ── State ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _summaryStats  = string.Empty;
    [ObservableProperty] private string _filterText    = string.Empty;
    [ObservableProperty] private string _downloadSpeedLabel = "↓ –";
    [ObservableProperty] private string _uploadSpeedLabel   = "↑ –";

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<PortSummaryRow>  PortRows           { get; } = [];
    public ObservableCollection<ConnectionRow>   Connections        { get; } = [];
    public ObservableCollection<ConnectionRow>   FilteredConnections { get; } = [];
    public ObservableCollection<MapPoint>        MapPoints          { get; } = [];

    // ── Real-time monitoring ──────────────────────────────────────────────────
    private DispatcherTimer? _timer;
    private long     _prevRecv, _prevSent;
    private DateTime _prevTime = DateTime.MinValue;

    public AnalyzerViewModel(ITcpConnectionReader reader, GeoLocationService geoService)
    {
        _reader     = reader;
        _geoService = geoService;
        StatusMessage = L["Analyzer.Status.Default"];
        L.LanguageChanged += () =>
        {
            if (!IsMonitoring && !IsRefreshing)
                StatusMessage = L["Analyzer.Status.Default"];
        };
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    // ── Commands ─────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        StatusMessage = L["Analyzer.Status.Fetching"];
        try
        {
            var all = await _reader.GetConnectionsAsync();

            var newIps = all
                .Where(c => c.State == "ESTABLISHED" &&
                            c.RemoteAddress != "0.0.0.0" &&
                            !GeoLocationService.IsPrivate(c.RemoteAddress) &&
                            _geoService.TryGet(c.RemoteAddress) is null)
                .Select(c => c.RemoteAddress)
                .Distinct()
                .ToList();

            if (newIps.Count > 0)
            {
                StatusMessage = string.Format(L["Analyzer.Status.GeoLooking"], newIps.Count);
                await _geoService.LookupAsync(newIps);
            }

            UpdateAll(all);
            int established = all.Count(c => c.State == "ESTABLISHED");
            StatusMessage = string.Format(L["Analyzer.Status.TcpInfo"], all.Count, established);
        }
        catch (Exception ex)
        {
            StatusMessage = $"{L["Analyzer.Status.Error"]}: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void StartMonitoring()
    {
        if (_timer != null) return;
        InitStats();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await OnTickAsync();
        _timer.Start();
        IsMonitoring = true;
        StatusMessage = L["Analyzer.Status.Monitoring"];
    }

    public void StopMonitoring()
    {
        _timer?.Stop();
        _timer = null;
        IsMonitoring    = false;
        DownloadSpeedLabel = "↓ –";
        UploadSpeedLabel   = "↑ –";
    }

    private void InitStats()
    {
        (_prevRecv, _prevSent) = GetTotalBytes();
        _prevTime = DateTime.UtcNow;
    }

    private async Task OnTickAsync()
    {
        try
        {
            UpdateBandwidth();
            await RefreshTickAsync();
        }
        catch { /* timer ticks must never crash the UI */ }
    }

    private void UpdateBandwidth()
    {
        var now     = DateTime.UtcNow;
        double elapsed = (now - _prevTime).TotalSeconds;
        if (elapsed < 0.5) return;

        var (recv, sent) = GetTotalBytes();
        double dl = Math.Max(0, (recv - _prevRecv) / elapsed);
        double ul = Math.Max(0, (sent - _prevSent) / elapsed);

        DownloadSpeedLabel = $"↓ {FormatSpeed(dl)}";
        UploadSpeedLabel   = $"↑ {FormatSpeed(ul)}";

        _prevRecv = recv;
        _prevSent = sent;
        _prevTime = now;
    }

    private async Task RefreshTickAsync()
    {
        if (IsRefreshing) return;
        var all = await _reader.GetConnectionsAsync();

        // Background geo lookup for new IPs (fire-and-forget)
        var newIps = all
            .Where(c => c.State == "ESTABLISHED" &&
                        c.RemoteAddress != "0.0.0.0" &&
                        !GeoLocationService.IsPrivate(c.RemoteAddress) &&
                        _geoService.TryGet(c.RemoteAddress) is null)
            .Select(c => c.RemoteAddress).Distinct().ToList();

        if (newIps.Count > 0)
            _ = _geoService.LookupAsync(newIps);

        UpdateAll(all);

        int established = all.Count(c => c.State == "ESTABLISHED");
        StatusMessage = $"TCP: {all.Count} wpisów  |  aktywnych: {established}  |  {DownloadSpeedLabel}  {UploadSpeedLabel}";
    }

    // ── Data update ───────────────────────────────────────────────────────────
    private void UpdateAll(IReadOnlyList<AnalizatorWiFi.Core.Models.TcpConnectionEntry> all)
    {
        var active = all.Where(c => c.State is "ESTABLISHED" or "CLOSE_WAIT").ToList();

        // Connections tab
        Connections.Clear();
        foreach (var c in active.OrderBy(c => c.RemotePort).ThenBy(c => c.RemoteAddress))
        {
            var geo = _geoService.TryGet(c.RemoteAddress);
            Connections.Add(new ConnectionRow
            {
                Protocol      = c.Protocol,
                LocalEndpoint = $"{c.LocalAddress}:{c.LocalPort}",
                RemoteEndpoint = $"{c.RemoteAddress}:{c.RemotePort}",
                State         = c.State,
                ServiceName   = PortNameService.GetName(c.RemotePort),
                Country       = geo?.Country ?? (GeoLocationService.IsPrivate(c.RemoteAddress) ? L["Geo.Local"] : ""),
                CountryCode   = geo?.CountryCode ?? "",
            });
        }
        ApplyFilter();

        // Summary tab
        PortRows.Clear();
        foreach (var g in active
            .Where(c => c.RemoteAddress != "0.0.0.0")
            .GroupBy(c => c.RemotePort)
            .OrderByDescending(g => g.Count())
            .Take(40))
        {
            PortRows.Add(new PortSummaryRow
            {
                Port            = g.Key,
                ServiceName     = PortNameService.GetName(g.Key),
                ConnectionCount = g.Count(),
                Countries       = string.Join(", ",
                    g.Select(c => _geoService.TryGet(c.RemoteAddress)?.Country)
                     .Where(ct => !string.IsNullOrEmpty(ct)).Distinct().Take(3)),
            });
        }

        int uniqueIps = active.Select(c => c.RemoteAddress).Distinct().Count();
        int uniqueCC  = active.Select(c => _geoService.TryGet(c.RemoteAddress)?.CountryCode)
                              .Where(cc => cc != null).Distinct().Count();
        SummaryStats = $"Aktywnych: {active.Count}  |  Unikalne IP: {uniqueIps}  |  Kraje: {uniqueCC}";

        // Map tab
        MapPoints.Clear();
        foreach (var g in active
            .Where(c => c.RemoteAddress != "0.0.0.0" && !GeoLocationService.IsPrivate(c.RemoteAddress))
            .GroupBy(c => _geoService.TryGet(c.RemoteAddress)?.CountryCode ?? "")
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderByDescending(g => g.Count()))
        {
            var geo = _geoService.TryGet(
                active.First(c => (_geoService.TryGet(c.RemoteAddress)?.CountryCode ?? "") == g.Key)
                      .RemoteAddress);
            if (geo is null) continue;
            MapPoints.Add(new MapPoint(geo.CountryCode, geo.Country, geo.Lat, geo.Lon, g.Count()));
        }
    }

    private void ApplyFilter()
    {
        FilteredConnections.Clear();
        var f = FilterText.Trim().ToLowerInvariant();
        foreach (var row in Connections)
        {
            if (string.IsNullOrEmpty(f) ||
                row.RemoteEndpoint.Contains(f) ||
                row.LocalEndpoint.Contains(f) ||
                row.Country.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                row.ServiceName.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                row.State.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                FilteredConnections.Add(row);
            }
        }
    }

    // ── Network stats (cross-platform) ────────────────────────────────────────
    private static (long recv, long sent) GetTotalBytes()
    {
        long recv = 0, sent = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback
                                        or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var s = ni.GetIPStatistics();
                recv += s.BytesReceived;
                sent += s.BytesSent;
            }
            catch { }
        }
        return (recv, sent);
    }

    private static string FormatSpeed(double bps)
    {
        if (bps < 1_024)           return $"{bps:F0} B/s";
        if (bps < 1_048_576)       return $"{bps / 1_024:F1} KB/s";
        return                            $"{bps / 1_048_576:F1} MB/s";
    }
}

// ── Row models ───────────────────────────────────────────────────────────────
public sealed class PortSummaryRow
{
    public int    Port            { get; init; }
    public string ServiceName     { get; init; } = string.Empty;
    public int    ConnectionCount { get; init; }
    public string Countries       { get; init; } = string.Empty;
    public string PortLabel => ServiceName.Length > 0 ? $"{Port}  ({ServiceName})" : Port.ToString();
}

public sealed class ConnectionRow
{
    public string Protocol       { get; init; } = string.Empty;
    public string LocalEndpoint  { get; init; } = string.Empty;
    public string RemoteEndpoint { get; init; } = string.Empty;
    public string State          { get; init; } = string.Empty;
    public string ServiceName    { get; init; } = string.Empty;
    public string Country        { get; init; } = string.Empty;
    public string CountryCode    { get; init; } = string.Empty;

    public string FlagEmoji => CountryCode.Length == 2
        ? string.Concat(CountryCode.Select(c => char.ConvertFromUtf32(0x1F1E0 + (char.ToUpperInvariant(c) - 'A'))))
        : "";
}

public sealed record MapPoint(string CountryCode, string Country, double Lat, double Lon, int Count);
