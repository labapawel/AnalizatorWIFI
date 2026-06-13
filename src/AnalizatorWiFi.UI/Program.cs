using Avalonia;
using AnalizatorWiFi.UI.Services;

namespace AnalizatorWiFi.UI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Build DI container before Avalonia starts
        ServiceLocator.BuildAsync().GetAwaiter().GetResult();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
