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

        var result = await CliTestHelper.RunAsync($"generate --spec \"{specPath}\" --output \"{outputDir}\" --output-style TypedClient --client-name MyApi");

        Assert.Equal(0, result.ExitCode);
        var csFiles = Directory.GetFiles(outputDir, "*Client.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);
        var clientContent = await File.ReadAllTextAsync(csFiles[0]);
        Assert.Contains("MyApi", clientContent);
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
}
