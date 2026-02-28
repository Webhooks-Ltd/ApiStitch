namespace ApiStitch.Configuration;

/// <summary>
/// Output style for the generated client code.
/// </summary>
public enum OutputStyle
{
    /// <summary>Typed HttpClient wrappers with deterministic structured folders.</summary>
    TypedClientStructured,

    /// <summary>Typed HttpClient wrappers emitted in a flat single folder.</summary>
    TypedClientFlat,
}
