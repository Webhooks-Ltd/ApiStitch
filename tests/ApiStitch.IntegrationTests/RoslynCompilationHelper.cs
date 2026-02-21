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
        var filesToCompile = excludeJsonContext
            ? files.Where(f => !f.RelativePath.Contains("JsonContext")).ToList()
            : files;

        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var syntaxTrees = filesToCompile
            .Select(f => CSharpSyntaxTree.ParseText(
                f.Content,
                parseOptions,
                path: f.RelativePath))
            .ToList();

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

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        return trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
