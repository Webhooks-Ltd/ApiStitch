#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PetStore.Client.Generated;

[JsonSerializable(typeof(CreatePetRequest))]
[JsonSerializable(typeof(PetStore.SharedModels.Owner))]
[JsonSerializable(typeof(PetStore.SharedModels.Pet))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(UploadPetPhotoRequest))]
[JsonSerializable(typeof(IReadOnlyList<PetStore.SharedModels.Owner>))]
[JsonSerializable(typeof(IReadOnlyList<PetStore.SharedModels.Pet>))]
[JsonSourceGenerationOptions]
public partial class GeneratedJsonContext : JsonSerializerContext;
