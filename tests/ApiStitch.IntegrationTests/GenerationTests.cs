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

    [Fact]
    public void ClientGeneration_InterfaceNamesAndMethodSignatures()
    {
        var result = Generate("petstore.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.Files, f => f.RelativePath == "IPetstoreApiPetsClient.cs");

        var (success, compileDiags, assembly) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);
        Assert.True(success, FormatDiagnostics(compileDiags));

        var interfaceType = assembly!.GetType("Generated.Models.IPetstoreApiPetsClient");
        Assert.NotNull(interfaceType);

        var methodNames = interfaceType.GetMethods().Select(m => m.Name).ToHashSet();
        Assert.Contains("ListPetsAsync", methodNames);
        Assert.Contains("CreatePetAsync", methodNames);
        Assert.Contains("GetPetByIdAsync", methodNames);
        Assert.Contains("DeletePetAsync", methodNames);
        Assert.Contains("UpdatePetAsync", methodNames);
    }

    [Fact]
    public void ClientGeneration_ImplementationClassesAreInternalSealed()
    {
        var result = Generate("petstore.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.Files, f => f.RelativePath == "PetstoreApiPetsClient.cs");

        var (success, compileDiags, assembly) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);
        Assert.True(success, FormatDiagnostics(compileDiags));

        var implType = assembly!.GetType("Generated.Models.PetstoreApiPetsClient");
        Assert.NotNull(implType);
        Assert.False(implType.IsPublic, "Implementation class should not be public");
        Assert.True(implType.IsSealed, "Implementation class should be sealed");
    }

    [Fact]
    public void ClientGeneration_DiRegistrationExtensionExists()
    {
        var result = Generate("petstore.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.Files, f => f.RelativePath == "PetstoreApiServiceCollectionExtensions.cs");

        var (success, compileDiags, assembly) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);
        Assert.True(success, FormatDiagnostics(compileDiags));

        var extType = assembly!.GetType("Generated.Models.PetstoreApiServiceCollectionExtensions");
        Assert.NotNull(extType);

        var addMethod = extType.GetMethods().FirstOrDefault(m => m.Name == "AddPetstoreApi");
        Assert.NotNull(addMethod);
    }

    [Fact]
    public void ClientGeneration_ApiExceptionStructure()
    {
        var result = Generate("petstore.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.Files, f => f.RelativePath == "ApiException.cs");

        var (success, compileDiags, assembly) = RoslynCompilationHelper.Compile(result.Files, excludeJsonContext: true);
        Assert.True(success, FormatDiagnostics(compileDiags));

        var exType = assembly!.GetType("Generated.Models.ApiException");
        Assert.NotNull(exType);
        Assert.True(typeof(HttpRequestException).IsAssignableFrom(exType));

        var responseBody = exType.GetProperty("ResponseBody");
        Assert.NotNull(responseBody);
        var responseHeaders = exType.GetProperty("ResponseHeaders");
        Assert.NotNull(responseHeaders);
    }

    [Fact]
    public void ClientGeneration_EnumExtensionsForQueryParameter()
    {
        var result = Generate("petstore.yaml");

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.Files, f => f.RelativePath == "PetStatusExtensions.cs");

        var enumExtFile = result.Files.First(f => f.RelativePath == "PetStatusExtensions.cs");
        Assert.Contains("ToQueryString", enumExtFile.Content);
    }

    [Fact]
    public void ClientGeneration_ModelOnlyWhenNoOperations()
    {
        var tempSpec = Path.Combine(Path.GetTempPath(), $"no-ops-{Guid.NewGuid()}.yaml");
        try
        {
            File.WriteAllText(tempSpec, """
                openapi: 3.0.3
                info:
                  title: ModelOnly
                  version: 1.0.0
                paths: {}
                components:
                  schemas:
                    Widget:
                      type: object
                      required: [id]
                      properties:
                        id:
                          type: integer
                          format: int32
                        name:
                          type: string
                """);

            var config = new ApiStitchConfig { Spec = tempSpec, Namespace = "Generated.Models" };
            var result = new GenerationPipeline().Generate(config);

            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.DoesNotContain(result.Files, f => f.RelativePath.EndsWith("Client.cs"));
            Assert.DoesNotContain(result.Files, f => f.RelativePath == "ApiException.cs");

            Assert.Contains(result.Files, f => f.RelativePath == "Widget.cs");
        }
        finally
        {
            if (File.Exists(tempSpec))
                File.Delete(tempSpec);
        }
    }

    [Fact]
    public void TypeReuse_ExternalSchemas_SkippedFromFiles_IncludedInContext()
    {
        var config = new ApiStitchConfig
        {
            Spec = SpecPath("type-reuse.yaml"),
            Namespace = "Generated.Models",
            TypeReuse = new TypeReuseConfig
            {
                IncludeNamespaces = ["SampleApi.*", "Microsoft.*"],
            },
        };
        var result = new GenerationPipeline().Generate(config);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Pet.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "PetStatus.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Category.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "CreatePetRequest.cs");

        var contextFile = result.Files.First(f => f.RelativePath.Contains("JsonContext"));
        Assert.Contains("SampleApi.Models.Pet", contextFile.Content);
        Assert.Contains("SampleApi.Models.PetStatus", contextFile.Content);
        Assert.Contains("Microsoft.AspNetCore.Mvc.ProblemDetails", contextFile.Content);
        Assert.Contains("CreatePetRequest", contextFile.Content);

        Assert.Contains("IReadOnlyList<SampleApi.Models.Pet>", contextFile.Content);

        var requestFile = result.Files.First(f => f.RelativePath == "CreatePetRequest.cs");
        Assert.Contains("partial record CreatePetRequest", requestFile.Content);
    }

    [Fact]
    public void TypeReuse_ExclusionConfig_ExcludedTypesRegenerated()
    {
        var config = new ApiStitchConfig
        {
            Spec = SpecPath("type-reuse.yaml"),
            Namespace = "Generated.Models",
            TypeReuse = new TypeReuseConfig
            {
                IncludeNamespaces = ["SampleApi.*", "Microsoft.*"],
                ExcludeNamespaces = ["Microsoft.AspNetCore.*"],
                ExcludeTypes = ["SampleApi.Models.PetStatus"],
            },
        };
        var result = new GenerationPipeline().Generate(config);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Pet.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStatus.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Category.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "CreatePetRequest.cs");

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.TypeReused);
    }

    [Fact]
    public void TypeReuse_ExternalBase_DerivedUsesFQN()
    {
        var config = new ApiStitchConfig
        {
            Spec = SpecPath("type-reuse-inheritance.yaml"),
            Namespace = "Generated.Models",
            TypeReuse = new TypeReuseConfig
            {
                IncludeNamespaces = ["SharedModels.*"],
            },
        };
        var result = new GenerationPipeline().Generate(config);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Animal.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Dog.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Cat.cs");

        var dogFile = result.Files.First(f => f.RelativePath == "Dog.cs");
        Assert.Contains("SharedModels.Animal", dogFile.Content);
        Assert.Contains("sealed", dogFile.Content);
    }

    [Fact]
    public void TypeReuse_ExternalDerived_BaseEmitsNormally()
    {
        var config = new ApiStitchConfig
        {
            Spec = SpecPath("type-reuse-inheritance.yaml"),
            Namespace = "Generated.Models",
            TypeReuse = new TypeReuseConfig
            {
                IncludeNamespaces = ["SharedModels.*"],
                ExcludeTypes = ["SharedModels.Animal"],
            },
        };
        var result = new GenerationPipeline().Generate(config);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.Files, f => f.RelativePath == "Animal.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Dog.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Cat.cs");

        var animalFile = result.Files.First(f => f.RelativePath == "Animal.cs");
        Assert.Contains("partial record Animal", animalFile.Content);
    }

    private static string FormatDiagnostics(IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> diagnostics) =>
        $"Compilation failed:\n{string.Join("\n", diagnostics.Select(d => $"  {d.Location}: {d.GetMessage()}"))}";
}
