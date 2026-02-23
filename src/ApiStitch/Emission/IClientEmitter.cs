using ApiStitch.Configuration;
using ApiStitch.Model;

namespace ApiStitch.Emission;

/// <summary>
/// Emits generated client code (interfaces, implementations, DI registration, etc.) from the API specification.
/// </summary>
public interface IClientEmitter
{
    /// <summary>
    /// Generates client files from the specification and configuration.
    /// </summary>
    EmissionResult Emit(ApiSpecification spec, ApiStitchConfig config);
}
