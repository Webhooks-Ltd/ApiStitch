using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Emission;
using ApiStitch.Parsing;
using ApiStitch.TypeMapping;

namespace ApiStitch.Generation;

/// <summary>
/// Orchestrates the full code generation pipeline from OpenAPI spec to generated files.
/// </summary>
public class GenerationPipeline
{
    private readonly IModelEmitter _modelEmitter;
    private readonly IClientEmitter? _clientEmitter;

    /// <summary>
    /// Creates a new pipeline with the specified emitters.
    /// </summary>
    public GenerationPipeline(IModelEmitter? modelEmitter = null, IClientEmitter? clientEmitter = null)
    {
        _modelEmitter = modelEmitter ?? new ScribanModelEmitter();
        _clientEmitter = clientEmitter ?? new ScribanClientEmitter();
    }

    /// <summary>
    /// Runs the generation pipeline for the given configuration.
    /// </summary>
    public GenerationResult Generate(ApiStitchConfig config)
    {
        return GenerateAsync(config, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the generation pipeline for the given configuration asynchronously.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(ApiStitchConfig config, CancellationToken cancellationToken = default)
    {
        var allDiagnostics = new List<Diagnostic>();

        if (string.IsNullOrWhiteSpace(config.Spec))
            return new GenerationResult([], [new Diagnostic(DiagnosticSeverity.Error, "AS100", "No spec path configured. Set 'spec' or 'project' in configuration.")]);

        var (document, loadDiagnostics) = await OpenApiSpecLoader.LoadAsync(config.Spec, cancellationToken).ConfigureAwait(false);
        allDiagnostics.AddRange(loadDiagnostics);

        if (document == null)
            return new GenerationResult([], allDiagnostics);

        var transformer = new SchemaTransformer();
        var (specification, schemaMap, transformDiagnostics) = transformer.Transform(document);
        allDiagnostics.AddRange(transformDiagnostics);

        InheritanceDetector.Detect(specification);

        var resolveDiagnostics = ExternalTypeResolver.Resolve(specification, config);
        allDiagnostics.AddRange(resolveDiagnostics);

        var (operations, syntheticResponseSchemas, clientName, opDiagnostics) = OperationTransformer.Transform(document, schemaMap, config);
        allDiagnostics.AddRange(opDiagnostics);

        var mergedSchemas = specification.Schemas
            .Concat(syntheticResponseSchemas)
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        specification = specification with
        {
            Schemas = mergedSchemas,
            Operations = operations,
            ClientName = clientName,
            HasProblemDetailsSupport = operations.Any(o => o.HasProblemDetailsSupport),
        };

        CSharpTypeMapper.MapAll(specification);

        if (operations.Count > 0)
        {
            var collectionTypes = GatherCollectionTypes(operations);
            specification = specification with { CollectionTypes = collectionTypes };
        }

        var allFiles = new List<GeneratedFile>();

        var modelResult = _modelEmitter.Emit(specification, config);
        allFiles.AddRange(modelResult.Files);
        allDiagnostics.AddRange(modelResult.Diagnostics);

        if (_clientEmitter is not null && specification.Operations.Count > 0)
        {
            var clientResult = _clientEmitter.Emit(specification, config);
            allFiles.AddRange(clientResult.Files);
            allDiagnostics.AddRange(clientResult.Diagnostics);
        }

        return new GenerationResult(allFiles, allDiagnostics);
    }

    private static IReadOnlyList<Model.ApiSchema> GatherCollectionTypes(IReadOnlyList<Model.ApiOperation> operations)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Model.ApiSchema>();

        foreach (var op in operations)
        {
            CollectArraySchema(op.SuccessResponse?.Schema, seen, result);
            CollectArraySchema(op.RequestBody?.Schema, seen, result);
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        return result;
    }

    private static void CollectArraySchema(Model.ApiSchema? schema, HashSet<string> seen, List<Model.ApiSchema> result)
    {
        if (schema is null || schema.Kind != Model.SchemaKind.Array)
            return;

        var itemName = schema.ArrayItemSchema?.CSharpTypeName ?? schema.ArrayItemSchema?.Name ?? "object";
        var key = $"IReadOnlyList<{itemName}>";

        if (seen.Add(key))
            result.Add(schema);
    }
}
