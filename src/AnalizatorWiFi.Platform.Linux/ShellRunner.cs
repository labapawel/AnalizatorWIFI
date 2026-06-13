using System.Diagnostics;
using System.Text;

namespace AnalizatorWiFi.Platform.Linux;

internal static class ShellRunner
{
    internal static async Task<(string Output, string Error, int ExitCode)> RunAsync(
        string command, string args, CancellationToken ct = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(ct);
        string error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return (output, error, process.ExitCode);
    }
}
