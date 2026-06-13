using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.UI.Services;
using AnalizatorWiFi.UI.ViewModels;
using AnalizatorWiFi.UI.Views;

namespace AnalizatorWiFi.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ServiceLocator.BuildAsync() was called in Program.cs before Avalonia starts
            var vm = ServiceLocator.Provider.GetRequiredService<MainWindowViewModel>();
            ApplyTheme(ServiceLocator.Provider.GetRequiredService<ISettingsService>().Current.Theme);

            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(AppTheme theme)
    {
        if (Current == null) return;
        Current.RequestedThemeVariant = theme switch
        {
            AppTheme.Dark  => ThemeVariant.Dark,
            AppTheme.Light => ThemeVariant.Light,
            _              => ThemeVariant.Default
        };
    }
}
