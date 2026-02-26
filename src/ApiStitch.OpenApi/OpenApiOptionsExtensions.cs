using Microsoft.AspNetCore.OpenApi;

namespace ApiStitch.OpenApi;

/// <summary>
/// Extension methods for registering ApiStitch type info enrichment on <see cref="OpenApiOptions"/>.
/// </summary>
public static class OpenApiOptionsExtensions
{
    /// <summary>
    /// Adds the ApiStitch schema transformer that writes <c>x-apistitch-type</c> extensions
    /// with CLR type names onto OpenAPI schemas.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> to configure.</param>
    /// <returns>The <paramref name="options"/> instance for chaining.</returns>
    public static OpenApiOptions AddApiStitchTypeInfo(this OpenApiOptions options)
    {
        return options.AddApiStitchTypeInfo(_ => { });
    }

    /// <summary>
    /// Adds the ApiStitch schema transformer that writes <c>x-apistitch-type</c> extensions
    /// with CLR type names onto OpenAPI schemas.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> to configure.</param>
    /// <param name="configure">A delegate to configure <see cref="ApiStitchTypeInfoOptions"/>.</param>
    /// <returns>The <paramref name="options"/> instance for chaining.</returns>
    public static OpenApiOptions AddApiStitchTypeInfo(this OpenApiOptions options, Action<ApiStitchTypeInfoOptions> configure)
    {
        var typeInfoOptions = new ApiStitchTypeInfoOptions();
        configure(typeInfoOptions);
        options.AddSchemaTransformer(new ApiStitchTypeInfoSchemaTransformer(typeInfoOptions));
        return options;
    }
}
