namespace AnalizatorWiFi.Platform.Windows.WlanApi;

internal sealed class WlanHandle : IDisposable
{
    public IntPtr Handle { get; }
    private bool _disposed;

    public WlanHandle()
    {
        uint result = NativeMethods.WlanOpenHandle(NativeMethods.WLAN_CLIENT_VERSION_VISTA, IntPtr.Zero, out _, out IntPtr handle);
        if (result != NativeMethods.ERROR_SUCCESS)
            throw new InvalidOperationException($"WlanOpenHandle failed: {result}");
        Handle = handle;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != IntPtr.Zero)
            NativeMethods.WlanCloseHandle(Handle, IntPtr.Zero);
    }
}
