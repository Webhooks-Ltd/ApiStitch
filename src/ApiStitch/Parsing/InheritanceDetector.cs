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

                var basePropertyNames = new HashSet<string>(
                    baseSchema.Properties.Select(p => p.Name), StringComparer.Ordinal);

                derived.Properties = derived.Properties
                    .Where(p => !basePropertyNames.Contains(p.Name))
                    .ToList();
            }
        }
    }
}
