using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Services;
using AnalizatorWiFi.UI.ViewModels;

namespace AnalizatorWiFi.UI.Services;

internal static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static IServiceProvider Provider => _provider ?? throw new InvalidOperationException("Services not initialized");

    public static async Task<IServiceProvider> BuildAsync()
    {
        var services = new ServiceCollection();

        // Settings
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string settingsPath = Path.Combine(appData, "AnalizatorWiFi", "settings.json");
        var settingsService = new SettingsService(settingsPath);
        await settingsService.LoadAsync();
        services.AddSingleton<ISettingsService>(settingsService);

        // Localisation — must happen before any ViewModel is created
        string langDir = Path.Combine(AppContext.BaseDirectory, "lang");
        LocalizationService.Instance.Discover(langDir);
        LocalizationService.Instance.Load(settingsService.Current.Language);
        services.AddSingleton(LocalizationService.Instance);

        // OUI lookup
        var ouiService = new OuiLookupService();
        LoadOuiData(ouiService);
        services.AddSingleton<IOuiLookup>(ouiService);

        // History
        services.AddSingleton<IHistoryRepository>(sp =>
        {
            var s = sp.GetRequiredService<ISettingsService>();
            return new SqliteHistoryRepository(s.Current.HistoryFilePath);
        });

        // Speed tester
        services.AddSingleton<ISpeedTester, IperfSpeedTester>();

        // Geo-location (shared HTTP client)
        services.AddSingleton<GeoLocationService>();

        // Platform services — selected at runtime
        RegisterPlatformServices(services, ouiService);

        // Connection monitor
        services.AddSingleton<IConnectionMonitor>(sp =>
            new ConnectionMonitorService(sp.GetRequiredService<IWifiConnector>()));

        // ViewModels
        services.AddTransient<NetworksViewModel>();
        services.AddTransient<SpectrumViewModel>();
        services.AddTransient<ConnectionViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SpeedTestViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AnalyzerViewModel>();
        services.AddTransient<MainWindowViewModel>();

        _provider = services.BuildServiceProvider();

        // Apply saved adapter to scanner and connector
        string savedAdapter = settingsService.Current.SelectedAdapter;
        if (!string.IsNullOrEmpty(savedAdapter))
        {
            _provider.GetRequiredService<IWifiScanner>().SetAdapter(savedAdapter);
            _provider.GetRequiredService<IWifiConnector>().SetAdapter(savedAdapter);
        }

        // Start connection monitor
        var monitor = _provider.GetRequiredService<IConnectionMonitor>();
        await monitor.StartAsync();

        return _provider;
    }

    private static void RegisterPlatformServices(IServiceCollection services, IOuiLookup ouiLookup)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IWifiScanner>(new Platform.Windows.WindowsWifiScanner(ouiLookup));
            services.AddSingleton<IWifiConnector, Platform.Windows.WindowsWifiConnector>();
            services.AddSingleton<ITcpConnectionReader, Platform.Windows.WindowsTcpConnectionReader>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IWifiScanner>(new Platform.Linux.LinuxWifiScanner(ouiLookup));
            services.AddSingleton<IWifiConnector, Platform.Linux.LinuxWifiConnector>();
            services.AddSingleton<ITcpConnectionReader, Platform.Linux.LinuxTcpConnectionReader>();
        }
        else
        {
            throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
        }
    }

    private static void LoadOuiData(OuiLookupService service)
    {
        string ouiPath = Path.Combine(AppContext.BaseDirectory, "oui.txt");
        if (File.Exists(ouiPath))
        {
            using var stream = File.OpenRead(ouiPath);
            service.LoadFromStream(stream);
        }
    }
}
