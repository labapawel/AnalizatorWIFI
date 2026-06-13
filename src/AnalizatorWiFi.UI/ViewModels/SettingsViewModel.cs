using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.UI.Services;

namespace AnalizatorWiFi.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IWifiScanner _scanner;

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private ScanMode _scanMode;
    [ObservableProperty] private int _scanIntervalSeconds;
    [ObservableProperty] private int _iperfDurationSeconds = 10;
    [ObservableProperty] private string _historyFilePath = string.Empty;
    [ObservableProperty] private int _maxHistoryDays = 30;
    [ObservableProperty] private bool _signalAlertEnabled;
    [ObservableProperty] private int _signalAlertThresholdDbm = -75;
    [ObservableProperty] private ObservableCollection<string> _adapters = [];
    [ObservableProperty] private string _selectedAdapter = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // iperf server list
    [ObservableProperty] private ObservableCollection<IperfServer> _iperfServers = [];
    [ObservableProperty] private string _newServerName    = string.Empty;
    [ObservableProperty] private string _newServerAddress = string.Empty;
    [ObservableProperty] private int    _newServerPort    = 5201;

    [ObservableProperty] private LanguageInfo? _selectedLanguage;

    public AppTheme[] Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];
    public ScanMode[] ScanModes { get; } = [ScanMode.Single, ScanMode.Continuous];
    public int[] ScanIntervals { get; } = [5, 10, 30, 60];
    public int[] DurationOptions { get; } = [10, 30, 60];
    public int[] HistoryDayOptions { get; } = [7, 30, 90, 0];

    public IReadOnlyList<LanguageInfo> AvailableLanguages =>
        LocalizationService.Instance.Available;

    public SettingsViewModel(ISettingsService settingsService, IWifiScanner scanner)
    {
        _settingsService = settingsService;
        _scanner = scanner;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Current;
        Theme                  = s.Theme;
        ScanMode               = s.ScanMode;
        ScanIntervalSeconds    = s.ScanIntervalSeconds;
        IperfDurationSeconds   = s.IperfDurationSeconds;
        HistoryFilePath        = s.HistoryFilePath;
        MaxHistoryDays         = s.MaxHistoryDays;
        SignalAlertEnabled     = s.SignalAlertEnabled;
        SignalAlertThresholdDbm = s.SignalAlertThresholdDbm;
        SelectedAdapter        = s.SelectedAdapter;
        IperfServers = new ObservableCollection<IperfServer>(s.IperfServers);
        SelectedLanguage = LocalizationService.Instance.Available
            .FirstOrDefault(l => l.Code == s.Language)
            ?? LocalizationService.Instance.Current;
    }

    partial void OnSelectedLanguageChanged(LanguageInfo? value)
    {
        if (value != null)
            LocalizationService.Instance.Load(value.Code);
    }

    [RelayCommand]
    private void AddServer()
    {
        if (string.IsNullOrWhiteSpace(NewServerAddress)) return;
        IperfServers.Add(new IperfServer
        {
            Name    = string.IsNullOrWhiteSpace(NewServerName) ? NewServerAddress : NewServerName.Trim(),
            Address = NewServerAddress.Trim(),
            Port    = NewServerPort
        });
        NewServerName    = string.Empty;
        NewServerAddress = string.Empty;
        NewServerPort    = 5201;
    }

    [RelayCommand]
    private void RemoveServer(IperfServer server) => IperfServers.Remove(server);

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settingsService.Current;
        s.Theme                   = Theme;
        s.ScanMode                = ScanMode;
        s.ScanIntervalSeconds     = ScanIntervalSeconds;
        s.IperfDurationSeconds    = IperfDurationSeconds;
        s.HistoryFilePath         = HistoryFilePath;
        s.MaxHistoryDays          = MaxHistoryDays;
        s.SignalAlertEnabled      = SignalAlertEnabled;
        s.SignalAlertThresholdDbm = SignalAlertThresholdDbm;
        s.SelectedAdapter         = SelectedAdapter;
        s.IperfServers            = IperfServers.ToList();
        s.Language                = SelectedLanguage?.Code ?? "pl";

        await _settingsService.SaveAsync();
        StatusMessage = L["Settings.Status.Saved"];
    }

    [RelayCommand]
    private async Task RefreshAdaptersAsync()
    {
        var adapters = await _scanner.GetAdaptersAsync();
        Adapters.Clear();
        foreach (var a in adapters) Adapters.Add(a);
    }
}
