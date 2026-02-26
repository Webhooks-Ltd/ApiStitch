using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApiStitch.OpenApi;

/// <summary>
/// An <see cref="IOpenApiSchemaTransformer"/> that writes <c>x-apistitch-type</c> vendor extensions
/// containing CLR type names onto OpenAPI schemas.
/// </summary>
internal sealed class ApiStitchTypeInfoSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly HashSet<Type> WellKnownTypes =
    [
        typeof(string), typeof(decimal), typeof(object),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly),
        typeof(TimeSpan), typeof(Guid), typeof(Uri), typeof(Half),
    ];

    private static readonly HashSet<Type> CollectionDefinitions =
    [
        typeof(List<>), typeof(IList<>), typeof(ICollection<>),
        typeof(IEnumerable<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>),
        typeof(HashSet<>), typeof(ISet<>), typeof(IReadOnlySet<>),
        typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
    ];

    private readonly ApiStitchTypeInfoOptions _options;

    public ApiStitchTypeInfoSchemaTransformer(ApiStitchTypeInfoOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (!IsUserDefinedType(type))
            return Task.CompletedTask;

        if (!ApiStitchDetection.IsOpenApiGenerationOnly && !_options.AlwaysEmit)
            return Task.CompletedTask;

        var typeName = GetCleanFullName(type);
        if (typeName is null)
            return Task.CompletedTask;

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.Extensions["x-apistitch-type"] = new JsonNodeExtension(typeName);

        return Task.CompletedTask;
    }

    internal static bool IsUserDefinedType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsArray) return false;
        if (t.IsPrimitive) return false;
        if (WellKnownTypes.Contains(t)) return false;
        if (t.IsGenericType && CollectionDefinitions.Contains(t.GetGenericTypeDefinition())) return false;
        if (t.FullName is null) return false;
        return true;
    }

    internal static string? GetCleanFullName(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (!t.IsGenericType)
            return t.FullName;

        var baseName = t.GetGenericTypeDefinition().FullName;
        if (baseName is null) return null;

        var backtickIndex = baseName.IndexOf('`');
        if (backtickIndex >= 0) baseName = baseName[..backtickIndex];

        var args = t.GetGenericArguments();
        var argNames = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var argName = GetCleanFullName(args[i]);
            if (argName is null) return null;
            argNames[i] = argName;
        }

        return $"{baseName}<{string.Join(", ", argNames)}>";
    }
}
