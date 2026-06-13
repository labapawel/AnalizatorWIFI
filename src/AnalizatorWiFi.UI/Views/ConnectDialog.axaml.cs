using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AnalizatorWiFi.UI.Views;

public partial class ConnectDialog : Window
{
    public sealed record DialogResult(string Ssid, string? Password);

    private readonly bool _needsPassword;
    private bool _passwordVisible;

    public ConnectDialog() => InitializeComponent();

    public ConnectDialog(string ssid, bool isHidden, bool needsPassword)
    {
        InitializeComponent();
        _needsPassword = needsPassword;

        SsidBox.Text       = ssid;
        SsidBox.IsReadOnly = !isHidden;

        PasswordPanel.IsVisible = needsPassword;
        OpenNotice.IsVisible    = !needsPassword;

        ConnectBtn.Click           += OnConnectClick;
        CancelBtn.Click            += OnCancelClick;
        ShowPasswordToggle.Tapped  += OnTogglePassword;

        // Select password box by default for quick entry
        if (needsPassword)
            PasswordBox.Focus();
        else
            ConnectBtn.Focus();
    }

    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        string ssid = SsidBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(ssid))
        {
            ShowError("Podaj nazwę sieci (SSID)");
            return;
        }

        string? password = _needsPassword ? PasswordBox.Text : null;
        Close(new DialogResult(ssid, string.IsNullOrEmpty(password) ? null : password));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnTogglePassword(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordBox.PasswordChar = _passwordVisible ? '\0' : '●';
        ShowPasswordToggle.Text  = _passwordVisible ? "Ukryj hasło" : "Pokaż hasło";
    }

    private void ShowError(string msg)
    {
        StatusText.Text      = msg;
        StatusText.IsVisible = true;
    }
}
