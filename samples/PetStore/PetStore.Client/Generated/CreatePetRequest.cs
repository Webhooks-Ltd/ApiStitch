#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Text.Json.Serialization;

namespace PetStore.Client.Generated;
[GeneratedCode("ApiStitch", null)]
public partial record CreatePetRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

}
