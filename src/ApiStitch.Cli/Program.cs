using System.CommandLine;
using System.CommandLine.Parsing;
using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;
using ApiStitch.IO;

var specOption = new Option<string?>("--spec") { Description = "Path or HTTP(S) URL to OpenAPI spec" };
var projectOption = new Option<string?>("--project") { Description = "Path to .csproj; the project will be built and run to extract its OpenAPI spec" };
var outputOption = new Option<string?>("--output") { Description = "Output directory for generated files" };
var namespaceOption = new Option<string?>("--namespace") { Description = "C# namespace for generated types" };
var clientNameOption = new Option<string?>("--client-name") { Description = "Client name override" };
var outputStyleOption = new Option<string?>("--output-style") { Description = "Output style: TypedClient" };
var configOption = new Option<string?>("--config") { Description = "Path to openapi-stitch.yaml config file" };
var cleanOutputOption = new Option<bool>("--clean-output") { Description = "Delete stale generated files from previous runs" };

var generateCommand = new Command("generate", "Generate C# client code from an OpenAPI spec")
{
    specOption,
    projectOption,
    outputOption,
    namespaceOption,
    clientNameOption,
    outputStyleOption,
    configOption,
    cleanOutputOption,
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

generateCommand.SetAction(async (parseResult, cancellationToken) =>
{
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

    try
    {
        var specArg = parseResult.GetValue(specOption);
        var projectArg = parseResult.GetValue(projectOption);
        var outputArg = parseResult.GetValue(outputOption);
        var namespaceArg = parseResult.GetValue(namespaceOption);
        var clientNameArg = parseResult.GetValue(clientNameOption);
        var outputStyleArg = parseResult.GetValue(outputStyleOption);
        var configArg = parseResult.GetValue(configOption);
        var cleanOutputArg = parseResult.GetValue(cleanOutputOption);

        string? yamlPath = null;
        if (configArg != null)
        {
            yamlPath = Path.GetFullPath(configArg);
        }
        else
        {
            var candidate = Path.Combine(Environment.CurrentDirectory, "openapi-stitch.yaml");
            if (File.Exists(candidate))
                yamlPath = candidate;
        }

        ApiStitchConfig? config = null;
        FileWriteOptions delivery = new();
        string displayOutputDir;
        var allDiagnostics = new List<Diagnostic>();

        if (yamlPath != null)
        {
            var (loadedConfig, loadedDelivery, diagnostics) = ConfigLoader.Load(yamlPath);
            allDiagnostics.AddRange(diagnostics);
            delivery = loadedDelivery;

            if (loadedConfig == null)
            {
                WriteDiagnostics(allDiagnostics);
                return 1;
            }

            var yamlDir = Path.GetDirectoryName(Path.GetFullPath(yamlPath))!;
            var resolvedSpec = specArg != null
                ? ResolveSpecInput(specArg, Environment.CurrentDirectory)
                : loadedConfig.Spec != null
                    ? ResolveSpecInput(loadedConfig.Spec, yamlDir)
                    : null;
            var resolvedOutput = outputArg != null
                ? Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, outputArg))
                : Path.GetFullPath(Path.Combine(yamlDir, loadedConfig.OutputDir));

            displayOutputDir = outputArg ?? loadedConfig.OutputDir;

            var outputStyle = loadedConfig.OutputStyle;
            if (outputStyleArg != null)
            {
                if (!Enum.TryParse<OutputStyle>(outputStyleArg, ignoreCase: true, out outputStyle))
                {
                    Console.Error.WriteLine($"error: Unknown output style '{outputStyleArg}'");
                    return 2;
                }
            }

            config = new ApiStitchConfig
            {
                Spec = resolvedSpec,
                Project = loadedConfig.Project,
                Namespace = namespaceArg ?? loadedConfig.Namespace,
                OutputDir = resolvedOutput,
                OutputStyle = outputStyle,
                ClientName = clientNameArg ?? loadedConfig.ClientName,
                TypeReuse = loadedConfig.TypeReuse,
            };
        }
        else
        {
            if (specArg == null && projectArg == null)
            {
                Console.Error.WriteLine("No openapi-stitch.yaml found in the current directory. Either create one or pass --spec or --project.");
                return 2;
            }

            displayOutputDir = outputArg ?? "./Generated";

            OutputStyle outputStyle = OutputStyle.TypedClient;
            if (outputStyleArg != null)
            {
                if (!Enum.TryParse<OutputStyle>(outputStyleArg, ignoreCase: true, out outputStyle))
                {
                    Console.Error.WriteLine($"error: Unknown output style '{outputStyleArg}'");
                    return 2;
                }
            }

            config = new ApiStitchConfig
            {
                Spec = specArg != null ? ResolveSpecInput(specArg, Environment.CurrentDirectory) : null,
                Project = projectArg,
                Namespace = namespaceArg ?? "ApiStitch.Generated",
                OutputDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, displayOutputDir)),
                OutputStyle = outputStyle,
                ClientName = clientNameArg,
            };
        }

        if (cleanOutputArg)
            delivery = new FileWriteOptions { CleanOutput = true };

        var resolvedProjectPath = projectArg != null
            ? Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, projectArg))
            : config.Project != null
                ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(yamlPath ?? "."))!, config.Project))
                : null;

        if (config.Spec == null && resolvedProjectPath != null)
        {
            var (extractedSpec, extractError) = await ApiStitch.Parsing.ProjectSpecExtractor.ExtractAsync(
                resolvedProjectPath, Console.Error, linked.Token);

            if (extractedSpec == null)
            {
                Console.Error.WriteLine($"error: {extractError}");
                return 1;
            }

            config = new ApiStitchConfig
            {
                Spec = extractedSpec,
                Namespace = config.Namespace,
                OutputDir = config.OutputDir,
                OutputStyle = config.OutputStyle,
                ClientName = config.ClientName,
                TypeReuse = config.TypeReuse,
            };
        }
        else if (config.Spec == null)
        {
            Console.Error.WriteLine("No spec or project specified. Use --spec, --project, or configure in openapi-stitch.yaml.");
            return 2;
        }

        var pipeline = new GenerationPipeline();
        var result = await pipeline.GenerateAsync(config, linked.Token);

        allDiagnostics.AddRange(result.Diagnostics);
        WriteDiagnostics(allDiagnostics);

        var hasErrors = allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        if (hasErrors)
            return 1;

        var writeResult = await FileWriter.WriteAsync(result.Files, config.OutputDir, delivery, linked.Token);

        WriteSummary(writeResult, displayOutputDir);
        return 0;
    }
    catch (OperationCanceledException)
    {
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
});

var rootCommand = new RootCommand("ApiStitch — OpenAPI client generator for .NET")
{
    generateCommand,
};

return await rootCommand.Parse(args).InvokeAsync();

static void WriteDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
{
    foreach (var d in diagnostics)
    {
        var severity = d.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info",
        };
        var specPath = d.SpecPath != null ? $" [{d.SpecPath}]" : "";
        Console.Error.WriteLine($"{severity} {d.Code}: {d.Message}{specPath}");
    }
}

static void WriteSummary(FileWriteResult result, string outputDir)
{
    var parts = new List<string>();
    if (result.Unchanged.Count > 0)
        parts.Add($"{result.Unchanged.Count} unchanged");
    if (result.Deleted.Count > 0)
        parts.Add($"{result.Deleted.Count} deleted");

    var suffix = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
    Console.WriteLine($"Generated {result.Written.Count} files in {outputDir}{suffix}");
}

static string ResolveSpecInput(string spec, string baseDirectory)
{
    if (LooksLikeRemoteHttpSpec(spec))
        return spec;

    return Path.GetFullPath(Path.Combine(baseDirectory, spec));
}

static bool LooksLikeRemoteHttpSpec(string spec)
{
    if (!Uri.TryCreate(spec, UriKind.Absolute, out var absolute))
        return false;

    return absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps;
}
