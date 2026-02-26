namespace ApiStitch.OpenApi;

/// <summary>
/// Options for controlling when <c>x-apistitch-type</c> extensions are emitted.
/// </summary>
public sealed class ApiStitchTypeInfoOptions
{
    /// <summary>
    /// When <c>true</c>, emit <c>x-apistitch-type</c> extensions at runtime in addition to build-time generation.
    /// Defaults to <c>false</c> (build-time only).
    /// </summary>
    public bool AlwaysEmit { get; set; }
}
