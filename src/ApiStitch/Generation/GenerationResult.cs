using ApiStitch.Diagnostics;

namespace ApiStitch.Generation;

public record GenerationResult(IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics);
