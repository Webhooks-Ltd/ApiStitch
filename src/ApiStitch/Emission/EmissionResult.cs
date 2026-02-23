using ApiStitch.Diagnostics;
using ApiStitch.Generation;

namespace ApiStitch.Emission;

/// <summary>
/// Result of an emission pass (model or client), containing generated files and diagnostics.
/// </summary>
/// <param name="Files">Generated source files.</param>
/// <param name="Diagnostics">Diagnostics produced during emission.</param>
public record EmissionResult(IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics);
