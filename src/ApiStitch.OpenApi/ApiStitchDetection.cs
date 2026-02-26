using System.Reflection;

namespace ApiStitch.OpenApi;

/// <summary>
/// Provides detection helpers for ApiStitch build-time scenarios.
/// </summary>
public static class ApiStitchDetection
{
    /// <summary>
    /// Returns <c>true</c> when the current process is running under
    /// <c>Microsoft.Extensions.ApiDescription.Server</c> for build-time OpenAPI spec generation.
    /// Use this to guard heavy startup dependencies (database, auth) that are not needed during spec generation.
    /// </summary>
    public static bool IsOpenApiGenerationOnly { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
