#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Text.Json.Serialization;

namespace PetStore.Client.Generated;
[GeneratedCode("ApiStitch", null)]
public partial record PetAvatar
{
    [JsonPropertyName("imageData")]
    public required byte[] ImageData { get; init; }

    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }

}
