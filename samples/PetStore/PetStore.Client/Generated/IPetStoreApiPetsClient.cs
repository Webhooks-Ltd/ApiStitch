#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
public partial interface IPetStoreApiPetsClient
{
    Task CreatePetAsync(CreatePetRequest body, CancellationToken cancellationToken = default);

    Task GetPetAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PetStore.SharedModels.Pet>> ListPetsAsync(CancellationToken cancellationToken = default);

}
