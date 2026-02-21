using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;

namespace ApiStitch.Tests.Generation;

public class GenerationPipelineTests
{
    private static string SpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Parsing", "Specs", fileName);

    [Fact]
    public void HappyPath_GeneratesFilesWithoutErrors()
    {
        var config = new ApiStitchConfig
        {
            Spec = SpecPath("minimal-valid.yaml"),
            Namespace = "TestApi.Models",
        };

        var pipeline = new GenerationPipeline();
        var result = pipeline.Generate(config);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotEmpty(result.Files);
        Assert.Contains(result.Files, f => f.RelativePath == "Pet.cs");
        Assert.Contains(result.Files, f => f.RelativePath.Contains("JsonContext"));
    }

    [Fact]
    public void TypedAdditionalProperties_EmitsDiagnostic()
    {
        var tempFile = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(tempFile, """
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Metadata:
                  type: object
                  properties:
                    name:
                      type: string
                  additionalProperties:
                    type: string
            """);
        try
        {
            var config = new ApiStitchConfig { Spec = tempFile, Namespace = "TestApi.Models" };
            var result = new GenerationPipeline().Generate(config);

            Assert.Contains(result.Diagnostics, d => d.Code == "AS205" && d.Severity == DiagnosticSeverity.Warning);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void MissingSpec_ReturnsErrorDiagnostic()
    {
        var config = new ApiStitchConfig
        {
            Spec = "/nonexistent/spec.yaml",
            Namespace = "TestApi.Models",
        };

        var pipeline = new GenerationPipeline();
        var result = pipeline.Generate(config);

        Assert.Empty(result.Files);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}
