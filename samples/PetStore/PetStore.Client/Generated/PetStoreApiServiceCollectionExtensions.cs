#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PetStore.Client.Generated;

/// <summary>
/// Extension methods for registering PetStoreApi services.
/// </summary>
[GeneratedCode("ApiStitch", null)]
public static class PetStoreApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the PetStoreApi HTTP client and all tag clients to the service collection.
    /// </summary>
    public static IHttpClientBuilder AddPetStoreApi(
        this IServiceCollection services,
        Action<PetStoreApiClientOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<PetStoreApiJsonOptions>();

        var builder = services.AddHttpClient("PetStoreApi", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PetStoreApiClientOptions>>().Value;
            if (options.BaseAddress is not null)
                client.BaseAddress = options.BaseAddress;
            if (options.Timeout is not null)
                client.Timeout = options.Timeout.Value;
            foreach (var header in options.DefaultHeaders)
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        });

        services.TryAddTransient<IPetStoreApiOwnersClient>(sp =>
            new PetStoreApiOwnersClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<PetStoreApiJsonOptions>()));
        services.TryAddTransient<IPetStoreApiPetsClient>(sp =>
            new PetStoreApiPetsClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<PetStoreApiJsonOptions>()));

        return builder;
    }
}
