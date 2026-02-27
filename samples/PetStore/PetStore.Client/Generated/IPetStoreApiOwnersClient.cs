#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
public partial interface IPetStoreApiOwnersClient
{
    Task<PetStore.SharedModels.Owner> GetOwnerAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PetStore.SharedModels.Owner>> ListOwnersAsync(CancellationToken cancellationToken = default);

}
