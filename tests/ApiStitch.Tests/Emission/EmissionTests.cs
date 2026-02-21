using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;

namespace ApiStitch.Tests.Emission;

public class EmissionTests
{
    private static string SpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Parsing", "Specs", fileName);

    private static GenerationResult Generate(string specFileName, string ns = "TestApi.Models")
    {
        var config = new ApiStitchConfig { Spec = SpecPath(specFileName), Namespace = ns };
        return new GenerationPipeline().Generate(config);
    }

    [Fact]
    public void SimpleRecord_SnapshotOutput()
    {
        var result = Generate("minimal-valid.yaml");
        var pet = result.Files.First(f => f.RelativePath == "Pet.cs");

        var expected = """
            #nullable enable
            using System;
            using System.CodeDom.Compiler;
            using System.Text.Json.Serialization;

            namespace TestApi.Models;
            [GeneratedCode("ApiStitch", null)]
            public partial record Pet
            {
                [JsonPropertyName("id")]
                public required long Id { get; init; }

                [JsonPropertyName("name")]
                public required string Name { get; init; }

            }

            """;

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(pet.Content));
    }

    [Fact]
    public void Enum_SnapshotOutput()
    {
        var result = GenerateFromInlineSpec("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                PetStatus:
                  type: string
                  enum: [active, inactive, pending]
            """);

        var petStatus = result.Files.First(f => f.RelativePath == "PetStatus.cs");

        var expected = """
            #nullable enable
            using System;
            using System.CodeDom.Compiler;
            using System.Runtime.Serialization;
            using System.Text.Json.Serialization;

            namespace TestApi.Models;

            [GeneratedCode("ApiStitch", null)]
            [JsonConverter(typeof(JsonStringEnumConverter<PetStatus>))]
            public enum PetStatus
            {
                [EnumMember(Value = "active")]
                Active,

                [EnumMember(Value = "inactive")]
                Inactive,

                [EnumMember(Value = "pending")]
                Pending

            }

            """;

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(petStatus.Content));
    }

    [Fact]
    public void JsonContext_SnapshotOutput()
    {
        var result = Generate("minimal-valid.yaml");
        var context = result.Files.First(f => f.RelativePath.Contains("JsonContext"));

        var expected = """
            #nullable enable
            using System.CodeDom.Compiler;
            using System.Text.Json.Serialization;

            namespace TestApi.Models;

            [JsonSerializable(typeof(Pet))]
            [GeneratedCode("ApiStitch", null)]
            [JsonSourceGenerationOptions]
            public partial class ModelsJsonContext : JsonSerializerContext;

            """;

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(context.Content));
    }

    [Fact]
    public void InheritedRecord_SealedWithBase()
    {
        var result = GenerateFromInlineSpec("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Animal:
                  type: object
                  properties:
                    name:
                      type: string
                  required: [name]
                Dog:
                  allOf:
                    - $ref: '#/components/schemas/Animal'
                    - type: object
                      properties:
                        breed:
                          type: string
                      required: [breed]
                Cat:
                  allOf:
                    - $ref: '#/components/schemas/Animal'
                    - type: object
                      properties:
                        indoor:
                          type: boolean
            """);

        var dog = result.Files.First(f => f.RelativePath == "Dog.cs");
        Assert.Contains("sealed partial record Dog : Animal", dog.Content);
        Assert.DoesNotContain("required string Name", dog.Content);
        Assert.Contains("Breed", dog.Content);

        var animal = result.Files.First(f => f.RelativePath == "Animal.cs");
        Assert.Contains("public partial record Animal", animal.Content);
        Assert.DoesNotContain("sealed", animal.Content);
    }

    [Fact]
    public void PropertiesFollowSpecDeclarationOrder()
    {
        var result = GenerateFromInlineSpec("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                User:
                  type: object
                  properties:
                    name:
                      type: string
                    id:
                      type: integer
                    email:
                      type: string
            """);

        var user = result.Files.First(f => f.RelativePath == "User.cs");
        var nameIdx = user.Content.IndexOf("Name", StringComparison.Ordinal);
        var idIdx = user.Content.IndexOf("Id", StringComparison.Ordinal);
        var emailIdx = user.Content.IndexOf("Email", StringComparison.Ordinal);

        Assert.True(nameIdx < idIdx, "Name should come before Id");
        Assert.True(idIdx < emailIdx, "Id should come before Email");
    }

    [Fact]
    public void FilesSortedAlphabetically()
    {
        var result = GenerateFromInlineSpec("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Zebra:
                  type: object
                  properties:
                    name:
                      type: string
                Apple:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var filePaths = result.Files.Select(f => f.RelativePath).ToList();
        var sorted = filePaths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, filePaths);
    }

    private static GenerationResult GenerateFromInlineSpec(string yaml)
    {
        var tempFile = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(tempFile, yaml);
        try
        {
            var config = new ApiStitchConfig { Spec = tempFile, Namespace = "TestApi.Models" };
            return new GenerationPipeline().Generate(config);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AdditionalProperties_SnapshotOutput()
    {
        var result = GenerateFromInlineSpec("""
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

        var metadata = result.Files.First(f => f.RelativePath == "Metadata.cs");

        var expected = """
            #nullable enable
            using System;
            using System.CodeDom.Compiler;
            using System.Text.Json;
            using System.Text.Json.Serialization;

            namespace TestApi.Models;
            [GeneratedCode("ApiStitch", null)]
            public partial record Metadata
            {
                [JsonPropertyName("name")]
                public string? Name { get; init; }

                // ApiStitch: Typed additionalProperties approximated as Dictionary<string, JsonElement>. [JsonExtensionData] only supports JsonElement.
                [JsonExtensionData]
                public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

            }

            """;

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(metadata.Content));
    }

    [Fact]
    public void DeprecatedSchema_SnapshotOutput()
    {
        var result = GenerateFromInlineSpec("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                OldModel:
                  type: object
                  deprecated: true
                  properties:
                    id:
                      type: integer
                      format: int64
                    field:
                      type: string
                      deprecated: true
                  required: [id]
            """);

        var old = result.Files.First(f => f.RelativePath == "OldModel.cs");

        var expected = """
            #nullable enable
            using System;
            using System.CodeDom.Compiler;
            using System.Text.Json.Serialization;

            namespace TestApi.Models;
            [Obsolete]
            [GeneratedCode("ApiStitch", null)]
            public partial record OldModel
            {
                [JsonPropertyName("id")]
                public required long Id { get; init; }

                [Obsolete]
                [JsonPropertyName("field")]
                public string? Field { get; init; }

            }

            """;

        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(old.Content));
    }

    [Fact]
    public void UntypedAdditionalProperties_NoComment()
    {
        var result = GenerateFromInlineSpec("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Dynamic:
                  type: object
                  properties:
                    name:
                      type: string
                  additionalProperties: {}
            """);

        var dynamic = result.Files.First(f => f.RelativePath == "Dynamic.cs");

        Assert.Contains("[JsonExtensionData]", dynamic.Content);
        Assert.Contains("Dictionary<string, JsonElement>? AdditionalProperties", dynamic.Content);
        Assert.DoesNotContain("// ApiStitch:", dynamic.Content);
    }

    private static string NormalizeLineEndings(string s) =>
        s.Replace("\r\n", "\n").TrimStart('\n');
}
