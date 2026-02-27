#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PetStore.Client.Generated;

[GeneratedCode("ApiStitch", null)]
public partial interface IPetStoreApiSystemClient
{
    Task<string> GetHealthAsync(CancellationToken cancellationToken = default);

}
