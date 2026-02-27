#nullable enable
using System.CodeDom.Compiler;
using System.Text.Json.Serialization;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
public sealed record ProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }
}
