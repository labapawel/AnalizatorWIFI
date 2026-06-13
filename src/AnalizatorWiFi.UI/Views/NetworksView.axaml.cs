using Avalonia.Controls;
using Avalonia.Interactivity;
using AnalizatorWiFi.Core.Models;
using AnalizatorWiFi.UI.ViewModels;

namespace AnalizatorWiFi.UI.Views;

public partial class NetworksView : UserControl
{
    public NetworksView() => InitializeComponent();

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WifiNetworkViewModel vm }) return;

        if (TopLevel.GetTopLevel(this) is not Window window) return;
        bool isHidden   = string.IsNullOrEmpty(vm.Model.Ssid);
        bool needsPass  = !vm.Model.Security.HasFlag(WifiSecurity.Open);

        var dialog = new ConnectDialog(vm.Model.Ssid ?? string.Empty, isHidden, needsPass);
        var result = await dialog.ShowDialog<ConnectDialog.DialogResult?>(window);

        if (result is null) return;
        if (DataContext is NetworksViewModel nvm)
            await nvm.ConnectToNetworkAsync(result.Ssid, result.Password);
    }
}
