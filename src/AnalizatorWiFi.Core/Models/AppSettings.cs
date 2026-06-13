namespace AnalizatorWiFi.Core.Models;

public enum AppTheme { System, Light, Dark }
public enum ScanMode { Single, Continuous }

public sealed class IperfServer
{
    public string Name    { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int    Port    { get; set; } = 5201;

    public override string ToString() =>
        string.IsNullOrEmpty(Name) ? $"{Address}:{Port}" : $"{Name}  ({Address}:{Port})";
}

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public ScanMode ScanMode { get; set; } = ScanMode.Single;
    public int ScanIntervalSeconds { get; set; } = 10;
    public int MaxScanIntervalSeconds { get; set; } = 60;

    public string IperfServerAddress { get; set; } = string.Empty;  // legacy – kept for compat
    public int IperfServerPort { get; set; } = 5201;
    public int IperfDurationSeconds { get; set; } = 10;
    public List<IperfServer> IperfServers { get; set; } = [];

    public string HistoryFilePath { get; set; } = string.Empty;
    public int MaxHistoryDays { get; set; } = 30;

    public string SelectedAdapter { get; set; } = string.Empty;

    public bool SignalAlertEnabled { get; set; } = false;
    public int SignalAlertThresholdDbm { get; set; } = -75;

    public string Language { get; set; } = "pl";
}
