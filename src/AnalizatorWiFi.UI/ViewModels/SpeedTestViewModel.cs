using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class SpeedTestViewModel : ViewModelBase
{
    private readonly ISpeedTester _speedTester;
    private readonly ISettingsService _settings;

    [ObservableProperty] private ObservableCollection<IperfServer> _servers = [];
    [ObservableProperty] private IperfServer? _selectedServer;
    [ObservableProperty] private IperfResult? _lastResult;
    [ObservableProperty] private PingResult? _pingResult;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusMessage = "Gotowy";
    [ObservableProperty] private int _duration = 10;

    public string DownloadLabel => LastResult?.IsSuccess == true ? $"{LastResult.DownloadMbps:F1} Mbps" : "–";
    public string UploadLabel   => LastResult?.IsSuccess == true ? $"{LastResult.UploadMbps:F1} Mbps"   : "–";
    public string JitterLabel   => LastResult?.IsSuccess == true ? $"{LastResult.JitterMs:F1} ms"        : "–";
    public string LostLabel     => LastResult?.IsSuccess == true ? $"{LastResult.LostPercent:F1}%"       : "–";

    public string PingAvgLabel  => PingResult?.IsSuccess == true ? $"{PingResult.AverageMs:F1} ms"  : "–";
    public string PingMinLabel  => PingResult?.IsSuccess == true ? $"{PingResult.MinMs:F1} ms"      : "–";
    public string PingMaxLabel  => PingResult?.IsSuccess == true ? $"{PingResult.MaxMs:F1} ms"      : "–";
    public string PingLostLabel => PingResult?.IsSuccess == true ? $"{PingResult.LostPercent:F0}%"  : "–";

    public SpeedTestViewModel(ISpeedTester speedTester, ISettingsService settings)
    {
        _speedTester = speedTester;
        _settings    = settings;
        Duration     = settings.Current.IperfDurationSeconds;
        RefreshServers();
    }

    public void RefreshServers()
    {
        var current = SelectedServer;
        Servers.Clear();
        foreach (var s in _settings.Current.IperfServers)
            Servers.Add(s);

        // Re-select by address match, or pick first
        SelectedServer = current != null
            ? Servers.FirstOrDefault(s => s.Address == current.Address && s.Port == current.Port)
              ?? Servers.FirstOrDefault()
            : Servers.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        if (SelectedServer == null)
        {
            StatusMessage = "Wybierz serwer iperf3 (dodaj go w Ustawieniach)";
            return;
        }
        IsRunning = true;
        StatusMessage = $"Trwa test do {SelectedServer.Name}...";
        try
        {
            LastResult = await _speedTester.RunAsync(SelectedServer.Address, SelectedServer.Port, Duration);
            StatusMessage = LastResult.IsSuccess
                ? $"Wynik: ↓{LastResult.DownloadMbps:F1} / ↑{LastResult.UploadMbps:F1} Mbps"
                : $"Błąd: {LastResult.ErrorMessage}";
        }
        finally
        {
            IsRunning = false;
            OnPropertyChanged(nameof(DownloadLabel));
            OnPropertyChanged(nameof(UploadLabel));
            OnPropertyChanged(nameof(JitterLabel));
            OnPropertyChanged(nameof(LostLabel));
        }
    }

    [RelayCommand]
    private async Task RunPingAsync()
    {
        string host = SelectedServer?.Address ?? "8.8.8.8";
        IsRunning = true;
        StatusMessage = $"Ping do {host}...";
        try
        {
            PingResult = await _speedTester.PingAsync(host);
            StatusMessage = PingResult.IsSuccess
                ? $"Ping: avg {PingResult.AverageMs:F1} ms"
                : $"Ping nieudany: {PingResult.ErrorMessage}";
        }
        finally
        {
            IsRunning = false;
            OnPropertyChanged(nameof(PingAvgLabel));
            OnPropertyChanged(nameof(PingMinLabel));
            OnPropertyChanged(nameof(PingMaxLabel));
            OnPropertyChanged(nameof(PingLostLabel));
        }
    }
}
