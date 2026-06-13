namespace AnalizatorWiFi.Core.Models;

[Flags]
public enum WifiSecurity
{
    Unknown = 0,
    Open    = 1 << 0,
    WEP     = 1 << 1,
    WPA     = 1 << 2,
    WPA2    = 1 << 3,
    WPA3    = 1 << 4,
    Enterprise = 1 << 5
}
