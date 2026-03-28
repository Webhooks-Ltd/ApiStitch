using ApiStitch.Model;

namespace ApiStitch.Parsing;

public static class InheritanceDetector
{
    public static void Detect(ApiSpecification specification)
    {
        var candidates = specification.Schemas
            .Where(s => s.AllOfRefTarget != null && s.HasAllOfInlineProperties)
            .ToList();

        var baseGroups = candidates
            .GroupBy(s => s.AllOfRefTarget!, ReferenceEqualityComparer.Instance)
            .Where(g => g.Count() >= 2)
            .ToList();

        foreach (var group in baseGroups)
        {
            var baseSchema = (ApiSchema)group.Key!;
            foreach (var derived in group)
            {
                derived.BaseSchema = baseSchema;

                var basePropertyNames = GetInheritedPropertyNames(baseSchema);

                derived.Properties = derived.Properties
                    .Where(p => !basePropertyNames.Contains(p.Name))
                    .ToList();
            }
        }
    }

    private static HashSet<string> GetInheritedPropertyNames(ApiSchema schema)
    {
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<ApiSchema>(ReferenceEqualityComparer.Instance);
        ApiSchema? current = schema;

        while (current is not null && visited.Add(current))
        {
            foreach (var property in current.Properties)
                propertyNames.Add(property.Name);

            current = current.BaseSchema;
        }

        return propertyNames;
    }
}
