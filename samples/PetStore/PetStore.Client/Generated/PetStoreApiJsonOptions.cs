#nullable enable
using System.CodeDom.Compiler;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
internal sealed class PetStoreApiJsonOptions
{
    public JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = GeneratedJsonContext.Default,
    };
}
