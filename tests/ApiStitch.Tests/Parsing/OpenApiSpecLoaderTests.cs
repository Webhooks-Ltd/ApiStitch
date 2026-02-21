using ApiStitch.Diagnostics;
using ApiStitch.Parsing;

namespace ApiStitch.Tests.Parsing;

public class OpenApiSpecLoaderTests
{
    private static string SpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Parsing", "Specs", fileName);

    [Fact]
    public void ValidYaml_ReturnsDocument()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("minimal-valid.yaml"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(doc.Components?.Schemas);
        Assert.Single(doc.Components.Schemas);
    }

    [Fact]
    public void ValidJson_ReturnsDocument()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("minimal-valid.json"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(doc.Components?.Schemas);
        Assert.Single(doc.Components.Schemas);
    }

    [Fact]
    public void MissingFile_ReturnsError()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load("/nonexistent/spec.yaml");

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS100", diag.Code);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    [Fact]
    public void SwaggerV2_ReturnsError()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("swagger-v2.yaml"));

        Assert.Null(doc);
        Assert.Contains(diagnostics, d => d.Code == "AS101" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void NoSchemas_ReturnsWarning()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("no-schemas.yaml"));

        Assert.NotNull(doc);
        Assert.Contains(diagnostics, d => d.Code == "AS103" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void OpenApi31_ReturnsError()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("minimal-valid-3.1.yaml"));

        Assert.Null(doc);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InvalidContent_ReturnsError()
    {
        var tempFile = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(tempFile, "this is not valid openapi content at all }{{}");
        try
        {
            var (doc, diagnostics) = OpenApiSpecLoader.Load(tempFile);

            Assert.True(
                doc == null || diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Should return null document or error diagnostics for invalid content");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
