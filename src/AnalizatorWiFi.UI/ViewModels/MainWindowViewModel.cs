using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.UI.Services;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public NetworksViewModel Networks { get; }
    public SpectrumViewModel Spectrum { get; }
    public ConnectionViewModel Connection { get; }
    public HistoryViewModel History { get; }
    public SpeedTestViewModel SpeedTest { get; }
    public AnalyzerViewModel Analyzer { get; }
    public SettingsViewModel Settings { get; }

    private readonly UpdateService _updateService;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private bool _isInstallingUpdate;
    [ObservableProperty] private int  _downloadProgress;
    [ObservableProperty] private string _updateBannerText = string.Empty;

    public MainWindowViewModel(
        NetworksViewModel networks,
        SpectrumViewModel spectrum,
        ConnectionViewModel connection,
        HistoryViewModel history,
        SpeedTestViewModel speedTest,
        AnalyzerViewModel analyzer,
        SettingsViewModel settings,
        UpdateService updateService)
    {
        Networks      = networks;
        Spectrum      = spectrum;
        Connection    = connection;
        History       = history;
        SpeedTest     = speedTest;
        Analyzer      = analyzer;
        Settings      = settings;
        _updateService = updateService;

        networks.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(NetworksViewModel.Networks)) return;
            var models = networks.Networks.Select(vm => vm.Model).ToList();
            Spectrum.UpdateNetworks(models);
            connection.SetAvailableNetworks(models);
        };

        _ = CheckForUpdateInBackgroundAsync();
    }

    private async Task CheckForUpdateInBackgroundAsync()
    {
        // Wait for the UI to be ready before showing any notification
        await Task.Delay(TimeSpan.FromSeconds(6));
        try
        {
            bool found = await _updateService.CheckForUpdatesAsync();
            if (found)
            {
                UpdateBannerText = string.Format(L["Update.Banner.Available"], _updateService.PendingVersion);
                IsUpdateAvailable = true;
            }
        }
        catch
        {
            // Silent — don't disturb the user if check fails
        }
    }

    [RelayCommand]
    private void DismissUpdateBanner() => IsUpdateAvailable = false;

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (!_updateService.HasPendingUpdate) return;
        IsInstallingUpdate = true;
        DownloadProgress = 0;
        UpdateBannerText = L["Update.Banner.Downloading"];
        try
        {
            await _updateService.DownloadUpdateAsync(p =>
            {
                DownloadProgress = p;
                UpdateBannerText = string.Format(L["Update.Banner.DownloadProgress"], p);
            });
            UpdateBannerText = L["Update.Banner.Restarting"];
            _updateService.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            UpdateBannerText = string.Format(L["Update.Banner.Error"], ex.Message);
            IsInstallingUpdate = false;
        }
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 3) _ = History.LoadBssidsAsync();
        if (value == 4) SpeedTest.RefreshServers();
        if (value == 5)
        {
            Analyzer.StartMonitoring();
            _ = Analyzer.RefreshAsync();
        }
        else if (Analyzer.IsMonitoring)
        {
            Analyzer.StopMonitoring();
        }
    }
}
