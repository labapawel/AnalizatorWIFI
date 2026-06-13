using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using AnalizatorWiFi.Core.Interfaces;
using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Services;

public sealed class IperfSpeedTester : ISpeedTester
{
    public async Task<IperfResult> RunAsync(string serverAddress, int port, int durationSeconds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
            return Fail(serverAddress, "Server address is not configured.");

        try
        {
            // Run download test
            var downResult = await RunIperfAsync(serverAddress, port, durationSeconds, reverse: true, ct);
            if (!downResult.IsSuccess) return downResult;

            // Run upload test
            var upResult = await RunIperfAsync(serverAddress, port, durationSeconds, reverse: false, ct);

            return new IperfResult
            {
                ServerAddress = serverAddress,
                DownloadMbps = downResult.DownloadMbps,
                UploadMbps = upResult.UploadMbps,
                JitterMs = upResult.JitterMs,
                LostPercent = upResult.LostPercent,
                DurationSeconds = durationSeconds,
                TestedAt = DateTimeOffset.Now,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            return Fail(serverAddress, ex.Message);
        }
    }

    private static async Task<IperfResult> RunIperfAsync(string server, int port, int duration, bool reverse, CancellationToken ct)
    {
        string reverseArg = reverse ? "-R" : string.Empty;
        string args = $"-c {server} -p {port} -t {duration} -J {reverseArg}".Trim();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "iperf3",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        string json = await process.StandardOutput.ReadToEndAsync(ct);
        string error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
            return Fail(server, string.IsNullOrEmpty(error) ? "iperf3 exited with error." : error);

        return ParseIperfJson(json, server, duration, reverse);
    }

    private static IperfResult ParseIperfJson(string json, string server, int duration, bool reverse)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var end = doc.RootElement.GetProperty("end");
            var streams = end.GetProperty("streams")[0];

            // TCP: sum -> bits_per_second; UDP: streams[0].udp -> bits_per_second + jitter_ms + lost_percent
            double bitsPerSec = 0;
            double jitter = 0;
            double lost = 0;

            if (end.TryGetProperty("sum_received", out var sumRec))
                bitsPerSec = sumRec.GetProperty("bits_per_second").GetDouble();
            else if (end.TryGetProperty("sum", out var sum))
                bitsPerSec = sum.GetProperty("bits_per_second").GetDouble();

            if (streams.TryGetProperty("udp", out var udp))
            {
                jitter = udp.GetProperty("jitter_ms").GetDouble();
                lost = udp.GetProperty("lost_percent").GetDouble();
            }

            double mbps = bitsPerSec / 1_000_000.0;

            return new IperfResult
            {
                ServerAddress = server,
                DownloadMbps = reverse ? mbps : 0,
                UploadMbps = reverse ? 0 : mbps,
                JitterMs = jitter,
                LostPercent = lost,
                DurationSeconds = duration,
                TestedAt = DateTimeOffset.Now,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            return Fail(server, $"Failed to parse iperf3 output: {ex.Message}");
        }
    }

    public async Task<PingResult> PingAsync(string host, int count = 10, CancellationToken ct = default)
    {
        using var ping = new Ping();
        var times = new List<long>();
        int lost = 0;

        for (int i = 0; i < count && !ct.IsCancellationRequested; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 2000);
                if (reply.Status == IPStatus.Success)
                    times.Add(reply.RoundtripTime);
                else
                    lost++;
            }
            catch { lost++; }

            if (i < count - 1) await Task.Delay(200, ct);
        }

        if (times.Count == 0)
            return new PingResult { Host = host, IsSuccess = false, ErrorMessage = "All packets lost", PacketsSent = count, LostPercent = 100 };

        return new PingResult
        {
            Host = host,
            AverageMs = times.Average(),
            MinMs = times.Min(),
            MaxMs = times.Max(),
            LostPercent = (double)lost / count * 100,
            PacketsSent = count,
            IsSuccess = true
        };
    }

    private static IperfResult Fail(string server, string msg) =>
        new() { ServerAddress = server, IsSuccess = false, ErrorMessage = msg, TestedAt = DateTimeOffset.Now };
}
