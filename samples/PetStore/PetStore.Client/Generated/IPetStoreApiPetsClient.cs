#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
public partial interface IPetStoreApiPetsClient
{
    Task CreatePetAsync(CreatePetRequest body, CancellationToken cancellationToken = default);

    Task<FileResponse> DownloadPetPhotoAsync(int id, CancellationToken cancellationToken = default);

    Task GetPetAsync(int id, CancellationToken cancellationToken = default);

    Task GetPetAvatarAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PetStore.SharedModels.Pet>> ListPetsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PetStore.SharedModels.Pet>> SearchPetsAsync(PetStore.SharedModels.PetStatus? status = null, IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default);

    Task SetPetAvatarAsync(int id, PetAvatar body, CancellationToken cancellationToken = default);

    Task UploadPetPhotoAsync(int id, Stream photo, string photoFileName, Stream? thumbnail = null, string? thumbnailFileName = null, string? description = null, CancellationToken cancellationToken = default);

}
