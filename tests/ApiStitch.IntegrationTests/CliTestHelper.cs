using System.Diagnostics;

namespace ApiStitch.IntegrationTests;

internal sealed record CliResult(int ExitCode, string Stdout, string Stderr);

internal static class CliTestHelper
{
    private static readonly string CliDll = ResolveCliDll();

    private static string ResolveCliDll()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var cliDir = Path.Combine(repoRoot, "src", "ApiStitch.Cli", "bin");

        foreach (var config in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(cliDir, config, "net10.0", "ApiStitch.Cli.dll");
            if (File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(cliDir, "Debug", "net10.0", "ApiStitch.Cli.dll");
    }

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
