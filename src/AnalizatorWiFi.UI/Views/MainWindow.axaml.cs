using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AnalizatorWiFi.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        FooterLink.Click += OnFooterLinkClick;
    }

    private static void OnFooterLinkClick(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://www.ebtech.pl") { UseShellExecute = true });
    }
}
