using System.Reflection;
using System.Text.Json;
using ApiStitch.Configuration;
using ApiStitch.Generation;

namespace ApiStitch.IntegrationTests;

public class DeserializationRoundTripTests
{
    private static string SpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Specs", fileName);

    [Fact]
    public void Petstore_DeserializationRoundTrip()
    {
        var config = new ApiStitchConfig
        {
            Spec = SpecPath("petstore.yaml"),
            Namespace = "RoundTrip.Models",
        };

        var result = new GenerationPipeline().Generate(config);
        var (success, compileDiags, assembly) = RoslynCompilationHelper.Compile(
            result.Files, assemblyName: "RoundTripTest", excludeJsonContext: true);

        Assert.True(success, $"Compilation failed:\n{string.Join("\n", compileDiags.Select(d => d.GetMessage()))}");
        Assert.NotNull(assembly);

        var petType = assembly.GetType("RoundTrip.Models.Pet")!;
        Assert.NotNull(petType);

        var json = """{"id":42,"name":"Fido","tag":"dog"}""";
        var pet = JsonSerializer.Deserialize(json, petType);
        Assert.NotNull(pet);

        var idProp = petType.GetProperty("Id")!;
        var nameProp = petType.GetProperty("Name")!;
        var tagProp = petType.GetProperty("Tag")!;

        Assert.Equal(42L, idProp.GetValue(pet));
        Assert.Equal("Fido", nameProp.GetValue(pet));
        Assert.Equal("dog", tagProp.GetValue(pet));

        var serialized = JsonSerializer.Serialize(pet, petType);
        Assert.Contains("\"id\":42", serialized);
        Assert.Contains("\"name\":\"Fido\"", serialized);
    }
}
