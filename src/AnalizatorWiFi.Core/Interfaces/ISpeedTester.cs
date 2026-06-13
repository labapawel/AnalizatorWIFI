using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface ISpeedTester
{
    Task<IperfResult> RunAsync(string serverAddress, int port, int durationSeconds, CancellationToken ct = default);
    Task<PingResult> PingAsync(string host, int count = 10, CancellationToken ct = default);
}
