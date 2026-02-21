using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Emission;
using ApiStitch.Parsing;
using ApiStitch.TypeMapping;

namespace ApiStitch.Generation;

public class GenerationPipeline
{
    private readonly IModelEmitter _emitter;

    public GenerationPipeline(IModelEmitter? emitter = null)
    {
        _emitter = emitter ?? new ScribanModelEmitter();
    }

    public GenerationResult Generate(ApiStitchConfig config)
    {
        var allDiagnostics = new List<Diagnostic>();

        var (document, loadDiagnostics) = OpenApiSpecLoader.Load(config.Spec);
        allDiagnostics.AddRange(loadDiagnostics);

        if (document == null)
            return new GenerationResult([], allDiagnostics);

        var transformer = new SchemaTransformer();
        var (specification, transformDiagnostics) = transformer.Transform(document);
        allDiagnostics.AddRange(transformDiagnostics);

        InheritanceDetector.Detect(specification);

        CSharpTypeMapper.MapAll(specification);

        var (files, emitDiagnostics) = _emitter.Emit(specification, config);
        allDiagnostics.AddRange(emitDiagnostics);

        return new GenerationResult(files, allDiagnostics);
    }
}
