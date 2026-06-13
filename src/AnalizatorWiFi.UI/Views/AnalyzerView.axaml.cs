using Avalonia.Controls;
using Avalonia.Interactivity;
using AnalizatorWiFi.UI.ViewModels;

namespace AnalizatorWiFi.UI.Views;

public partial class AnalyzerView : UserControl
{
    public AnalyzerView()
    {
        InitializeComponent();
    }

    private void OnMonitoringToggle(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AnalyzerViewModel vm) return;
        if (vm.IsMonitoring) vm.StopMonitoring();
        else vm.StartMonitoring();
    }
}
