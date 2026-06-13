namespace AnalizatorWiFi.Core.Models;

public sealed record TcpConnectionEntry
{
    public string Protocol { get; init; } = "TCP";
    public string LocalAddress { get; init; } = string.Empty;
    public int LocalPort { get; init; }
    public string RemoteAddress { get; init; } = string.Empty;
    public int RemotePort { get; init; }
    public string State { get; init; } = string.Empty;
    public int ProcessId { get; init; }
}
