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

        if (excludeJsonContext)
        {
            filesToCompile = filesToCompile
                .Where(f => !f.RelativePath.Contains("JsonContext") && !f.RelativePath.Contains("JsonOptions"));
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
            var ns = DetectNamespace(filteredFiles);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                BuildJsonOptionsStub(ns, filteredFiles),
                parseOptions,
                path: "JsonOptionsStub.cs"));
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

    private static string BuildJsonOptionsStub(string ns, List<GeneratedFile> files)
    {
        var className = "JsonOptionsStub";
        foreach (var f in files)
        {
            if (!f.RelativePath.EndsWith("Client.cs")) continue;
            var match = System.Text.RegularExpressions.Regex.Match(f.Content, @"(\w+JsonOptions)\s+jsonOptions");
            if (match.Success)
            {
                className = match.Groups[1].Value;
                break;
            }
        }

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
