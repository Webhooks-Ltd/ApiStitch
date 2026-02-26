using ApiStitch.Diagnostics;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace ApiStitch.Parsing;

public static class OpenApiSpecLoader
{
    public static (OpenApiDocument? Document, IReadOnlyList<Diagnostic> Diagnostics) Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS100", $"OpenAPI spec file not found: {filePath}", filePath)]);
        }

        try
        {
            var content = File.ReadAllText(filePath);
            var settings = new OpenApiReaderSettings();
            settings.AddYamlReader();
            var result = OpenApiDocument.Parse(content, settings: settings);

            var diagnostics = new List<Diagnostic>();
            var document = result.Document;

            foreach (var error in result.Diagnostic?.Errors ?? [])
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "AS100",
                    error.Message,
                    error.Pointer));
            }

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return (null, diagnostics);

            foreach (var warning in result.Diagnostic?.Warnings ?? [])
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "AS100",
                    warning.Message,
                    warning.Pointer));
            }

            if (document?.Components?.Schemas == null || document.Components.Schemas.Count == 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS103",
                    "OpenAPI spec has no component schemas. No types will be generated.", filePath));
            }

            return (document, diagnostics);
        }
        catch (Exception ex)
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS100", $"Failed to read OpenAPI spec: {ex.Message}", filePath)]);
        }
    }
}
