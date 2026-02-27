#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Text.Json.Serialization;

namespace PetStore.Client.Generated;
[GeneratedCode("ApiStitch", null)]
public partial record UploadPetPhotoRequest
{
    [JsonPropertyName("photo")]
    public required byte[] Photo { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

}
