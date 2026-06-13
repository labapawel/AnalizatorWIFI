namespace AnalizatorWiFi.Core.Models;

public enum WifiStandard
{
    Unknown,
    Dot11b,        // WiFi 1 - 2.4GHz
    Dot11a,        // WiFi 2 - 5GHz
    Dot11g,        // WiFi 3 - 2.4GHz
    Dot11n,        // WiFi 4 - 2.4/5GHz
    Dot11ac,       // WiFi 5 - 5GHz
    Dot11ax,       // WiFi 6/6E - 2.4/5/6GHz
    Dot11be        // WiFi 7 - 2.4/5/6GHz
}
