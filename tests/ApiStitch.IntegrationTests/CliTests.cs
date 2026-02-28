using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ApiStitch.IntegrationTests;

public class CliTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "apistitch-cli-test-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _specsDir;

    public CliTests()
    {
        Directory.CreateDirectory(_tempDir);
        _specsDir = Path.Combine(AppContext.BaseDirectory, "Specs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GenerateFromYamlConfig_WritesFilesAndExitCode0()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");
        var yamlContent = $"""
            spec: {specPath.Replace("\\", "/")}
            namespace: PetStore.Client
            outputDir: {outputDir.Replace("\\", "/")}
            """;
        var yamlPath = Path.Combine(_tempDir, "openapi-stitch.yaml");
        await File.WriteAllTextAsync(yamlPath, yamlContent);

        var result = await CliTestHelper.RunAsync($"generate --config \"{yamlPath}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Generated", result.Stdout);
        Assert.True(Directory.Exists(outputDir));
        Assert.NotEmpty(Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task GenerateWithCliArgs_WritesFilesAndExitCode0()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Generated", result.Stdout);
        Assert.True(Directory.Exists(outputDir));
        Assert.NotEmpty(Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task NoConfigNoSpec_ErrorMessageAndExitCode2()
    {
        var result = await CliTestHelper.RunAsync("generate", _tempDir);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("No openapi-stitch.yaml found", result.Stderr);
    }

    [Fact]
    public async Task CliArgsOverrideYaml_NamespaceOverridden()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");
        var yamlContent = $"""
            spec: {specPath.Replace("\\", "/")}
            namespace: Original.Namespace
            outputDir: {outputDir.Replace("\\", "/")}
            """;
        var yamlPath = Path.Combine(_tempDir, "openapi-stitch.yaml");
        await File.WriteAllTextAsync(yamlPath, yamlContent);

        var result = await CliTestHelper.RunAsync($"generate --config \"{yamlPath}\" --namespace Override.Namespace");

        Assert.Equal(0, result.ExitCode);
        var csFiles = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        var content = await File.ReadAllTextAsync(csFiles[0]);
        Assert.Contains("Override.Namespace", content);
        Assert.DoesNotContain("Original.Namespace", content);
    }

    [Fact]
    public async Task YamlSpecPath_ResolvesRelativeToYamlDir()
    {
        var specSubDir = Path.Combine(_tempDir, "specs");
        Directory.CreateDirectory(specSubDir);
        File.Copy(Path.Combine(_specsDir, "petstore.yaml"), Path.Combine(specSubDir, "petstore.yaml"));

        var configDir = Path.Combine(_tempDir, "config");
        Directory.CreateDirectory(configDir);
        var outputDir = Path.Combine(_tempDir, "output");
        var yamlContent = $"""
            spec: ../specs/petstore.yaml
            outputDir: {outputDir.Replace("\\", "/")}
            """;
        var yamlPath = Path.Combine(configDir, "openapi-stitch.yaml");
        await File.WriteAllTextAsync(yamlPath, yamlContent);

        var result = await CliTestHelper.RunAsync($"generate --config \"{yamlPath}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(Directory.Exists(outputDir));
    }

    [Fact]
    public async Task CleanOutput_DeletesStaleFiles()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");

        await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\" --clean-output");

        var staleFile = Path.Combine(outputDir, "Stale.cs");
        await File.WriteAllTextAsync(staleFile, "stale");
        var manifest = Path.Combine(outputDir, ".apistitch.manifest");
        var existingManifest = await File.ReadAllTextAsync(manifest);
        await File.WriteAllTextAsync(manifest, existingManifest + "Stale.cs\n");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\" --clean-output");

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(staleFile));
        Assert.Contains("deleted", result.Stdout);
    }

    [Fact]
    public async Task DiagnosticsWrittenToStderr()
    {
        var badSpecPath = Path.Combine(_tempDir, "nonexistent.yaml");
        var outputDir = Path.Combine(_tempDir, "output");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{badSpecPath}\" --output \"{outputDir}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("error", result.Stderr);
    }

    [Fact]
    public async Task SummaryLineWrittenToStdout()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"Generated \d+ files in", result.Stdout);
    }

    [Fact]
    public async Task UnhandledException_CleanErrorExitCode1()
    {
        var specPath = Path.Combine(_tempDir, "bad.yaml");
        await File.WriteAllTextAsync(specPath, "not a valid openapi spec at all");
        var outputDir = Path.Combine(_tempDir, "output");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("   at ", result.Stderr);
    }

    [Fact]
    public async Task VersionFlag_OutputsVersionAndExitCode0()
    {
        var result = await CliTestHelper.RunAsync("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", result.Stdout);
    }

    [Fact]
    public async Task OutputStyleAndClientName_OverrideDefaults()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\" --output-style TypedClientFlat --client-name MyApi");

        Assert.Equal(0, result.ExitCode);
        var csFiles = Directory.GetFiles(outputDir, "*Client.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);
        var clientContent = await File.ReadAllTextAsync(csFiles[0]);
        Assert.Contains("MyApi", clientContent);
    }

    [Fact]
    public async Task InvalidOutputStyle_ShowsSupportedValuesAndExitCode2()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output-invalid-style");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\" --output-style InvalidStyle");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown output style 'InvalidStyle'", result.Stderr);
        Assert.Contains("TypedClientStructured", result.Stderr);
        Assert.Contains("TypedClientFlat", result.Stderr);
    }

    [Fact]
    public async Task DefaultOutputStyle_IsStructuredLayout()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output-structured-default");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(outputDir, "Clients")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "Contracts")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "Models")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "Infrastructure")));
        Assert.True(Directory.Exists(Path.Combine(outputDir, "Configuration")));
    }

    [Fact]
    public async Task DefaultsWhenOnlySpecProvided()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "Generated");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        var csFiles = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        var content = await File.ReadAllTextAsync(csFiles[0]);
        Assert.Contains("ApiStitch.Generated", content);
    }

    [Fact]
    public async Task WarningsStillExitCode0()
    {
        var specPath = Path.Combine(_specsDir, "petstore.yaml");
        var outputDir = Path.Combine(_tempDir, "output");

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\"");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task GenerateWithRemoteSpecUrl_PassesUrlToLoader()
    {
        var outputDir = Path.Combine(_tempDir, "output-remote");
        var url = "https://127.0.0.1:9/openapi.yaml";

        var result = await CliTestHelper.RunAsync($"generate --spec \"{url}\" --output \"{outputDir}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Failed to fetch OpenAPI spec URL", result.Stderr);
        Assert.DoesNotContain("OpenAPI spec file not found", result.Stderr);
    }

    [Fact]
    public async Task GenerateWithRemoteSpecUrl_Succeeds()
    {
        var outputDir = Path.Combine(_tempDir, "output-remote-success");
        var specContent = await File.ReadAllTextAsync(Path.Combine(_specsDir, "petstore.yaml"));

        await using var server = await SingleResponseHttpServer.StartAsync(specContent, "application/yaml");
        var result = await CliTestHelper.RunAsync($"generate --spec \"{server.Url}\" --output \"{outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Generated", result.Stdout);
        Assert.NotEmpty(Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CliSpecUrl_OverridesYamlLocalSpec()
    {
        var localMissing = Path.Combine(_tempDir, "missing-local.yaml");
        var outputDir = Path.Combine(_tempDir, "output-override-url");
        var yamlPath = Path.Combine(_tempDir, "openapi-stitch.yaml");
        var url = "https://127.0.0.1:9/openapi.yaml";

        var yamlContent = $"""
            spec: {localMissing.Replace("\\", "/")}
            namespace: PetStore.Client
            outputDir: {outputDir.Replace("\\", "/")}
            """;
        await File.WriteAllTextAsync(yamlPath, yamlContent);

        var result = await CliTestHelper.RunAsync($"generate --config \"{yamlPath}\" --spec \"{url}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Failed to fetch OpenAPI spec URL", result.Stderr);
        Assert.DoesNotContain(localMissing, result.Stderr);
    }

    private sealed class SingleResponseHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _response;
        private readonly CancellationTokenSource _cts;
        private readonly Task _serveTask;

        private SingleResponseHttpServer(TcpListener listener, string response, CancellationTokenSource cts)
        {
            _listener = listener;
            _response = response;
            _cts = cts;
            _serveTask = Task.Run(ServeLoopAsync);
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/openapi.yaml";

        public static async Task<SingleResponseHttpServer> StartAsync(string content, string contentType)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var bytes = Encoding.UTF8.GetBytes(content);
            var headers = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
            var response = headers + content;
            var cts = new CancellationTokenSource();
            var server = new SingleResponseHttpServer(listener, response, cts);
            await Task.Yield();
            return server;
        }

        private async Task ServeLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    await using var stream = client.GetStream();

                    var buffer = new byte[4096];
                    _ = await stream.ReadAsync(buffer, _cts.Token);

                    var responseBytes = Encoding.UTF8.GetBytes(_response);
                    await stream.WriteAsync(responseBytes, _cts.Token);
                    await stream.FlushAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
                finally
                {
                    client?.Dispose();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                await _serveTask;
            }
            catch
            {
            }
            _cts.Dispose();
        }
    }
}
