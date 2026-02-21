using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;
using ApiStitch.Model;

namespace ApiStitch.Emission;

public interface IModelEmitter
{
    (IReadOnlyList<GeneratedFile> Files, IReadOnlyList<Diagnostic> Diagnostics) Emit(ApiSpecification spec, ApiStitchConfig config);
}
