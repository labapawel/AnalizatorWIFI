using CommunityToolkit.Mvvm.ComponentModel;

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

    [ObservableProperty] private int _selectedTabIndex;

    public MainWindowViewModel(
        NetworksViewModel networks,
        SpectrumViewModel spectrum,
        ConnectionViewModel connection,
        HistoryViewModel history,
        SpeedTestViewModel speedTest,
        AnalyzerViewModel analyzer,
        SettingsViewModel settings)
    {
        Networks = networks;
        Spectrum = spectrum;
        Connection = connection;
        History = history;
        SpeedTest = speedTest;
        Analyzer = analyzer;
        Settings = settings;

        networks.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(NetworksViewModel.Networks)) return;
            var models = networks.Networks.Select(vm => vm.Model).ToList();
            Spectrum.UpdateNetworks(models);
            connection.SetAvailableNetworks(models);
        };
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
