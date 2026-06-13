using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly IWifiConnector _connector;
    private readonly IConnectionMonitor _monitor;
    private readonly ISpeedTester _speedTester;
    private IReadOnlyList<WifiNetwork> _availableNetworks = [];

    [ObservableProperty] private ConnectionInfo? _current;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _targetSsid = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isWorking;
    [ObservableProperty] private PingResult? _lastPing;

    public string SsidLabel       => string.IsNullOrEmpty(Current?.Ssid) ? "–" : Current.Ssid;
    public string IpLabel         => Current?.IpAddress ?? "–";
    public string GatewayLabel    => Current?.Gateway ?? "–";
    public string BssidLabel      => Current?.Bssid ?? "–";
    public string DnsLabel        => Current?.DnsServers.Any() == true ? string.Join(", ", Current.DnsServers) : "–";
    public string LinkSpeedLabel  => Current != null ? $"{Current.LinkSpeedMbps} Mbps" : "–";
    public string SignalLabel     => Current != null ? $"{Current.SignalDbm} dBm" : "–";
    public string ChannelLabel    => Current?.Channel > 0 ? Current.Channel.ToString() : "–";
    public string DistanceLabel   => Current != null ? $"{EstimateDistance(Current.SignalDbm):F1} m" : "–";
    public string StandardLabel   => Current?.Standard switch
    {
        WifiStandard.Dot11b  => "802.11b",
        WifiStandard.Dot11a  => "802.11a",
        WifiStandard.Dot11g  => "802.11g",
        WifiStandard.Dot11n  => "WiFi 4 (802.11n)",
        WifiStandard.Dot11ac => "WiFi 5 (802.11ac)",
        WifiStandard.Dot11ax => "WiFi 6 (802.11ax)",
        WifiStandard.Dot11be => "WiFi 7 (802.11be)",
        _ => "–"
    };

    public string PingAvgLabel  => LastPing?.IsSuccess == true ? $"{LastPing.AverageMs:F1} ms" : "–";
    public string PingMinLabel  => LastPing?.IsSuccess == true ? $"{LastPing.MinMs:F1} ms"     : "–";
    public string PingMaxLabel  => LastPing?.IsSuccess == true ? $"{LastPing.MaxMs:F1} ms"     : "–";
    public string PingLostLabel => LastPing?.IsSuccess == true ? $"{LastPing.LostPercent:F0}%" : "–";

    public ConnectionViewModel(IWifiConnector connector, IConnectionMonitor monitor, ISpeedTester speedTester)
    {
        _connector   = connector;
        _monitor     = monitor;
        _speedTester = speedTester;

        _monitor.ConnectionChanged.Subscribe(info =>
        {
            Current     = FillMissingBssid(info);
            IsConnected = Current != null;
            NotifyLabels();
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsWorking = true;
        try
        {
            var info = FillMissingBssid(await _connector.GetCurrentConnectionAsync());
            Current     = info;
            IsConnected = info != null;
            NotifyLabels();
            StatusMessage = info != null ? $"Odświeżono o {info.CapturedAt:HH:mm:ss}" : "Brak aktywnego połączenia";
        }
        finally { IsWorking = false; }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetSsid)) return;
        IsWorking = true;
        StatusMessage = $"Łączenie z {TargetSsid}...";
        try
        {
            bool ok = await _connector.ConnectAsync(TargetSsid, string.IsNullOrEmpty(Password) ? null : Password);
            StatusMessage = ok ? $"Połączono z {TargetSsid}" : "Nie udało się połączyć";
        }
        finally { IsWorking = false; }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        IsWorking = true;
        StatusMessage = "Rozłączanie...";
        try
        {
            await _connector.DisconnectAsync();
            StatusMessage = "Rozłączono";
        }
        finally { IsWorking = false; }
    }

    [RelayCommand]
    private async Task PingGatewayAsync()
    {
        string host = Current?.Gateway ?? string.Empty;
        if (string.IsNullOrEmpty(host)) return;
        IsWorking = true;
        StatusMessage = $"Ping do {host}...";
        try
        {
            LastPing = await _speedTester.PingAsync(host);
            OnPropertyChanged(nameof(PingAvgLabel));
            OnPropertyChanged(nameof(PingMinLabel));
            OnPropertyChanged(nameof(PingMaxLabel));
            OnPropertyChanged(nameof(PingLostLabel));
            StatusMessage = LastPing.IsSuccess
                ? $"Ping: avg {LastPing.AverageMs:F1} ms, utrata {LastPing.LostPercent:F0}%"
                : $"Ping nieudany: {LastPing.ErrorMessage}";
        }
        finally { IsWorking = false; }
    }

    public void ConnectToNetwork(string ssid)
    {
        TargetSsid = ssid;
        Password   = string.Empty;
    }

    public void SetAvailableNetworks(IEnumerable<WifiNetwork> networks)
        => _availableNetworks = networks.ToList();

    private ConnectionInfo? FillMissingBssid(ConnectionInfo? info)
    {
        if (info == null || !string.IsNullOrEmpty(info.Bssid)) return info;

        // Try to find BSSID in last scan by matching SSID + channel
        var match = _availableNetworks.FirstOrDefault(n =>
            n.Channel == info.Channel &&
            string.Equals(n.Ssid, info.Ssid, StringComparison.OrdinalIgnoreCase));

        if (match == null && !string.IsNullOrEmpty(info.Ssid))
            // Fallback: match only by SSID, take strongest signal
            match = _availableNetworks
                .Where(n => string.Equals(n.Ssid, info.Ssid, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.SignalDbm)
                .FirstOrDefault();

        return match != null ? info with { Bssid = match.Bssid } : info;
    }

    private void NotifyLabels()
    {
        OnPropertyChanged(nameof(SsidLabel));
        OnPropertyChanged(nameof(BssidLabel));
        OnPropertyChanged(nameof(IpLabel));
        OnPropertyChanged(nameof(GatewayLabel));
        OnPropertyChanged(nameof(DnsLabel));
        OnPropertyChanged(nameof(LinkSpeedLabel));
        OnPropertyChanged(nameof(SignalLabel));
        OnPropertyChanged(nameof(ChannelLabel));
        OnPropertyChanged(nameof(DistanceLabel));
        OnPropertyChanged(nameof(StandardLabel));
    }

    private static double EstimateDistance(int signalDbm)
    {
        if (signalDbm >= 0) return 0;
        return Math.Pow(10.0, (-40.0 - signalDbm) / 20.0);
    }
}
