using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;

namespace ApiStitch.IntegrationTests;

public class GenerationTests
{
    private static string SpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    private static GenerationResult Generate(string specFile, string ns = "Generated.Models")
    {
        var config = new ApiStitchConfig { Spec = SpecPath(specFile), Namespace = ns };
        return new GenerationPipeline().Generate(config);
    }

    [Fact]
    public void Petstore_GeneratesAndCompiles()
    {
        var result = Generate("petstore.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotEmpty(result.Files);

        var (success, compileDiags, _) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);

        Assert.True(success, FormatDiagnostics(compileDiags));
    }

    [Fact]
    public void ComplexMicroservice_GeneratesAndCompiles()
    {
        var result = Generate("complex-microservice.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var (success, compileDiags, _) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);

        Assert.True(success, FormatDiagnostics(compileDiags));
    }

    [Fact]
    public void AllOfComposition_GeneratesAndCompiles()
    {
        var result = Generate("allof-composition.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var (success, compileDiags, _) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);

        Assert.True(success, FormatDiagnostics(compileDiags));

        var animalFile = result.Files.First(f => f.RelativePath == "Animal.cs");
        Assert.Contains("partial record Animal", animalFile.Content);
    }

    [Fact]
    public void EdgeCases_GeneratesAndCompiles()
    {
        var result = Generate("edge-cases.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var (success, compileDiags, _) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);

        Assert.True(success, FormatDiagnostics(compileDiags));
    }

    [Fact]
    public void Determinism_IdenticalOutput()
    {
        var result1 = Generate("petstore.yaml");
        var result2 = Generate("petstore.yaml");

        Assert.Equal(result1.Files.Count, result2.Files.Count);

        for (var i = 0; i < result1.Files.Count; i++)
        {
            Assert.Equal(result1.Files[i].RelativePath, result2.Files[i].RelativePath);
            Assert.Equal(result1.Files[i].Content, result2.Files[i].Content);
        }
    }

    [Fact]
    public void JsonContext_IsGenerated()
    {
        var result = Generate("petstore.yaml");

        var context = result.Files.FirstOrDefault(f => f.RelativePath.Contains("JsonContext"));
        Assert.NotNull(context);
        Assert.Contains("JsonSerializerContext", context.Content);
        Assert.Contains("[JsonSerializable(typeof(Pet))]", context.Content);
        Assert.Contains("[JsonSerializable(typeof(Category))]", context.Content);
        Assert.Contains("[JsonSerializable(typeof(PetStatus))]", context.Content);
    }

    private static string FormatDiagnostics(IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> diagnostics) =>
        $"Compilation failed:\n{string.Join("\n", diagnostics.Select(d => $"  {d.Location}: {d.GetMessage()}"))}";
}
