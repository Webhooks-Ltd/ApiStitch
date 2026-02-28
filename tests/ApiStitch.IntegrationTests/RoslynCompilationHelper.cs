using System.Reflection;
using ApiStitch.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ApiStitch.IntegrationTests;

internal static class RoslynCompilationHelper
{
    public static (bool Success, IReadOnlyList<Diagnostic> Diagnostics, Assembly? Assembly) Compile(
        IReadOnlyList<GeneratedFile> files,
        string assemblyName = "TestGenerated",
        bool excludeJsonContext = false)
    {
        var filesToCompile = files.AsEnumerable();
        var excludedJsonOptionsFiles = new List<GeneratedFile>();

        if (excludeJsonContext)
        {
            excludedJsonOptionsFiles = files
                .Where(f => f.RelativePath.Contains("JsonOptions", StringComparison.Ordinal))
                .ToList();

            filesToCompile = filesToCompile
                .Where(f => !f.RelativePath.Contains("JsonContext", StringComparison.Ordinal)
                    && !f.RelativePath.Contains("JsonOptions", StringComparison.Ordinal));
        }

        var filteredFiles = filesToCompile.ToList();

        var hasClientFiles = filteredFiles.Any(f => f.RelativePath.EndsWith("Client.cs"));

        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var syntaxTrees = filteredFiles
            .Select(f => CSharpSyntaxTree.ParseText(
                f.Content,
                parseOptions,
                path: f.RelativePath))
            .ToList();

        if (excludeJsonContext && hasClientFiles)
        {
            var stubs = BuildJsonOptionsStubs(excludedJsonOptionsFiles, filteredFiles);
            foreach (var stub in stubs)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                    stub.Content,
                    parseOptions,
                    path: stub.Path));
            }
        }

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        var diagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if (!result.Success)
            return (false, diagnostics, null);

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        return (true, diagnostics, assembly);
    }

    private static IReadOnlyList<(string Path, string Content)> BuildJsonOptionsStubs(
        List<GeneratedFile> excludedJsonOptionsFiles,
        List<GeneratedFile> filesToCompile)
    {
        var stubs = new List<(string Path, string Content)>();
        var added = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in excludedJsonOptionsFiles)
        {
            var namespaceMatch = System.Text.RegularExpressions.Regex.Match(file.Content, @"namespace\s+([^;\s]+)");
            var classMatch = System.Text.RegularExpressions.Regex.Match(file.Content, @"internal\s+sealed\s+class\s+(\w+JsonOptions)");
            if (!namespaceMatch.Success || !classMatch.Success)
                continue;

            var ns = namespaceMatch.Groups[1].Value;
            var className = classMatch.Groups[1].Value;
            var key = $"{ns}|{className}";
            if (!added.Add(key))
                continue;

            stubs.Add(($"{className}.Stub.cs", BuildJsonOptionsStub(ns, className)));
        }

        if (stubs.Count > 0)
            return stubs;

        var fallbackNamespace = DetectNamespace(filesToCompile);
        stubs.Add(("JsonOptionsStub.cs", BuildJsonOptionsStub(fallbackNamespace, "JsonOptionsStub")));
        return stubs;
    }

    private static string DetectNamespace(List<GeneratedFile> files)
    {
        foreach (var f in files)
        {
            var idx = f.Content.IndexOf("namespace ", StringComparison.Ordinal);
            if (idx < 0) continue;
            var end = f.Content.IndexOfAny([';', '{', '\r', '\n'], idx + 10);
            if (end < 0) continue;
            return f.Content[(idx + 10)..end].Trim();
        }
        return "Generated.Models";
    }

    private static string BuildJsonOptionsStub(string ns, string className)
    {
        return $$"""
            #nullable enable
            using System.Text.Json;
            namespace {{ns}};
            internal sealed class {{className}}
            {
                public JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
            }
            """;
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        return trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
