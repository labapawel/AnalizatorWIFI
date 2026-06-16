using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.UI.ViewModels;


public partial class NetworksViewModel : ViewModelBase
{
    private readonly IWifiScanner _scanner;
    private readonly IHistoryRepository _history;
    private readonly ISettingsService _settings;
    private readonly IWifiConnector _connector;
    private CancellationTokenSource? _continuousCts;

    [ObservableProperty] private ObservableCollection<WifiNetworkViewModel> _networks = [];
    public ObservableCollection<object> GroupedNetworks { get; } = [];
    public ObservableCollection<string> Adapters { get; } = [];

    private object? _selectedGroupedItem;
    public object? SelectedGroupedItem
    {
        get => _selectedGroupedItem;
        set { if (SetProperty(ref _selectedGroupedItem, value)) SelectedNetwork = value as WifiNetworkViewModel; }
    }

    [ObservableProperty] private WifiNetworkViewModel? _selectedNetwork;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusMessage = "Gotowy";
    [ObservableProperty] private string _selectedBand = "Wszystkie";
    [ObservableProperty] private string? _selectedAdapter;
    [ObservableProperty] private bool _isContinuous;
    [ObservableProperty] private DateTimeOffset _lastScanTime;

    public ObservableCollection<string> BandFilters { get; } = ["Wszystkie", "2.4 GHz", "5 GHz", "6 GHz"];

    public NetworksViewModel(IWifiScanner scanner, IHistoryRepository history, ISettingsService settings, IWifiConnector connector)
    {
        _scanner = scanner;
        _history = history;
        _settings = settings;
        _connector = connector;
        _isContinuous = settings.Current.ScanMode == ScanMode.Continuous;
        _selectedAdapter = settings.Current.SelectedAdapter;
        _ = LoadAdaptersAsync();
    }

    private async Task LoadAdaptersAsync()
    {
        try
        {
            var list = await _scanner.GetAdaptersAsync();
            Adapters.Clear();
            foreach (var a in list) Adapters.Add(a);
        }
        catch { }
    }

    partial void OnSelectedAdapterChanged(string? value)
    {
        string adapter = value ?? string.Empty;
        _scanner.SetAdapter(adapter);
        _connector.SetAdapter(adapter);
        _settings.Current.SelectedAdapter = adapter;
        _ = _settings.SaveAsync();
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid, string? password)
    {
        StatusMessage = $"Łączenie z {ssid}...";
        bool ok = await _connector.ConnectAsync(ssid, password);
        StatusMessage = ok ? $"Połączono z {ssid}" : $"Nie udało się połączyć z {ssid}";
        return ok;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        StatusMessage = "Skanowanie...";

        try
        {
            var result = await _scanner.ScanAsync();
            ApplyResults(result);
            if (result.IsSuccess)
                await _history.SaveScanAsync(result);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task StartContinuousAsync()
    {
        if (_continuousCts != null) return;
        _continuousCts = new CancellationTokenSource();
        var token = _continuousCts.Token;
        int interval = _settings.Current.ScanIntervalSeconds * 1000;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await ScanAsync();
                await Task.Delay(interval, token); // no ConfigureAwait(false) — must stay on UI thread
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop — not an error
        }
        finally
        {
            _continuousCts?.Dispose();
            _continuousCts = null;
        }
    }

    [RelayCommand]
    private void StopContinuous()
    {
        _continuousCts?.Cancel();
        StatusMessage = "Zatrzymano";
    }

    partial void OnSelectedBandChanged(string value) => ApplyBandFilter();

    [RelayCommand]
    private async Task RefreshAdaptersAsync()
    {
        await LoadAdaptersAsync();
    }

    private void ApplyResults(ScanResult result)
    {
        if (!result.IsSuccess) { StatusMessage = $"Błąd: {result.ErrorMessage}"; return; }

        LastScanTime = result.ScannedAt;
        var filtered = FilterByBand(result.Networks).ToList();

        // Flat list for Spectrum tab
        Networks.Clear();
        foreach (var n in filtered.OrderByDescending(n => n.SignalDbm))
            Networks.Add(new WifiNetworkViewModel(n));
        OnPropertyChanged(nameof(Networks));

        // Grouped list for Networks tab
        GroupedNetworks.Clear();
        var groups = filtered
            .GroupBy(n => string.IsNullOrEmpty(n.Ssid) ? "<ukryta>" : n.Ssid)
            .OrderBy(g => g.Key == "<ukryta>" ? "￿" : g.Key);

        foreach (var g in groups)
        {
            GroupedNetworks.Add(new NetworkGroupHeader(g.Key, g.Count()));
            foreach (var n in g.OrderByDescending(x => x.SignalDbm))
                GroupedNetworks.Add(new WifiNetworkViewModel(n));
        }

        StatusMessage = $"Znaleziono {result.Networks.Count} sieci  |  {result.ScannedAt:HH:mm:ss}";
    }

    private void ApplyBandFilter()
    {
        // Re-trigger last scan result — just filter in-memory for now via re-scan or keep cached
    }

    private IEnumerable<WifiNetwork> FilterByBand(IEnumerable<WifiNetwork> networks) =>
        SelectedBand switch
        {
            "2.4 GHz" => networks.Where(n => n.Band == WifiBand.GHz2_4),
            "5 GHz"   => networks.Where(n => n.Band == WifiBand.GHz5),
            "6 GHz"   => networks.Where(n => n.Band == WifiBand.GHz6),
            _         => networks
        };
}

public sealed class NetworkGroupHeader
{
    public string Ssid  { get; }
    public int    Count { get; }
    public NetworkGroupHeader(string ssid, int count) { Ssid = ssid; Count = count; }
}

public partial class WifiNetworkViewModel : ViewModelBase
{
    public WifiNetwork Model { get; }

    public string Ssid => string.IsNullOrEmpty(Model.Ssid) ? "<ukryta>" : Model.Ssid;
    public string Bssid => Model.Bssid;
    public string Vendor => Model.Vendor;
    public int SignalDbm => Model.SignalDbm;
    public int SignalPercent => Model.SignalPercent;
    public int Channel => Model.Channel;
    public string FrequencyGhz => $"{Model.FrequencyMhz / 1000.0:F3} GHz";
    public string BandLabel => Model.Band switch { WifiBand.GHz2_4 => "2.4", WifiBand.GHz5 => "5", WifiBand.GHz6 => "6", _ => "?" };
    public string SecurityLabel => FormatSecurity(Model.Security);
    public string StandardLabel => Model.Standard switch
    {
        WifiStandard.Dot11b => "802.11b", WifiStandard.Dot11a => "802.11a",
        WifiStandard.Dot11g => "802.11g", WifiStandard.Dot11n => "WiFi 4",
        WifiStandard.Dot11ac => "WiFi 5", WifiStandard.Dot11ax => "WiFi 6",
        WifiStandard.Dot11be => "WiFi 7", _ => "?"
    };
    public string DistanceLabel => $"{Model.EstimatedDistanceMeters:F1} m";
    public string ChannelWidthLabel => $"{Model.ChannelWidthMhz} MHz";
    public string SignalBarColor => SignalDbm >= -60 ? "#27ae60" : SignalDbm >= -70 ? "#f39c12" : "#e74c3c";

    public WifiNetworkViewModel(WifiNetwork model) => Model = model;

    private static string FormatSecurity(WifiSecurity sec)
    {
        if (sec == WifiSecurity.Open) return "Otwarta";
        var parts = new List<string>();
        if (sec.HasFlag(WifiSecurity.WPA3)) parts.Add("WPA3");
        if (sec.HasFlag(WifiSecurity.WPA2)) parts.Add("WPA2");
        if (sec.HasFlag(WifiSecurity.WPA))  parts.Add("WPA");
        if (sec.HasFlag(WifiSecurity.WEP))  parts.Add("WEP");
        if (sec.HasFlag(WifiSecurity.Enterprise)) parts.Add("802.1X");
        return string.Join("/", parts);
    }
}
