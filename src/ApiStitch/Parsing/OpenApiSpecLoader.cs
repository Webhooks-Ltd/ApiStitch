using ApiStitch.Diagnostics;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace ApiStitch.Parsing;

public static class OpenApiSpecLoader
{
    public static (OpenApiDocument? Document, IReadOnlyList<Diagnostic> Diagnostics) Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS100", $"OpenAPI spec file not found: {filePath}", filePath)]);
        }

        OpenApiDocument document;
        OpenApiDiagnostic openApiDiagnostic;
        try
        {
            using var stream = File.OpenRead(filePath);
            document = new OpenApiStreamReader().Read(stream, out openApiDiagnostic);
        }
        catch (Exception ex)
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS100", $"Failed to read OpenAPI spec: {ex.Message}", filePath)]);
        }

        var diagnostics = new List<Diagnostic>();

        if (openApiDiagnostic.SpecificationVersion == OpenApiSpecVersion.OpenApi2_0)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "AS101",
                "OpenAPI 2.0 (Swagger) is not supported. Please convert to OpenAPI 3.0.", filePath));
            return (null, diagnostics);
        }

        foreach (var error in openApiDiagnostic.Errors)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                "AS100",
                error.Message,
                error.Pointer));
        }

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return (null, diagnostics);

        foreach (var warning in openApiDiagnostic.Warnings)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                "AS100",
                warning.Message,
                warning.Pointer));
        }

        if (document.Components?.Schemas == null || document.Components.Schemas.Count == 0)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "AS103",
                "OpenAPI spec has no component schemas. No types will be generated.", filePath));
        }

        return (document, diagnostics);
    }
}
