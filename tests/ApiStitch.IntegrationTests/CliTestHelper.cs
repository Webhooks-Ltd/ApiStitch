using System.Diagnostics;

namespace ApiStitch.IntegrationTests;

internal sealed record CliResult(int ExitCode, string Stdout, string Stderr);

internal static class CliTestHelper
{
    private static readonly string CliDll = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "ApiStitch.Cli", "bin", "Debug", "net8.0", "ApiStitch.Cli.dll"));

    public static async Task<CliResult> RunAsync(string arguments, string? workingDirectory = null, int timeoutMs = 30000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{CliDll} {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeoutMs);
        await process.WaitForExitAsync(cts.Token);

        return new CliResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
