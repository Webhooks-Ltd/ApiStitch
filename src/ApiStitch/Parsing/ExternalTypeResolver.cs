using System.Text.RegularExpressions;
using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Model;

namespace ApiStitch.Parsing;

/// <summary>
/// Resolves vendor type hints to external CLR type names, applying include/exclude configuration.
/// Types are only reused if they match an include pattern. Exclude patterns override includes.
/// </summary>
public static class ExternalTypeResolver
{
    /// <summary>
    /// Iterates all schemas, evaluates VendorTypeHint against include/exclude config, and sets ExternalClrTypeName.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Resolve(ApiSpecification specification, ApiStitchConfig config)
    {
        var diagnostics = new List<Diagnostic>();

        if (!config.TypeReuse.HasIncludeRules)
            return diagnostics;

        var includePatterns = BuildPatterns(config.TypeReuse.IncludeNamespaces);
        var includeTypes = new HashSet<string>(config.TypeReuse.IncludeTypes, StringComparer.Ordinal);
        var excludePatterns = BuildPatterns(config.TypeReuse.ExcludeNamespaces);
        var excludeTypes = new HashSet<string>(config.TypeReuse.ExcludeTypes, StringComparer.Ordinal);

        foreach (var schema in specification.Schemas)
        {
            if (schema.VendorTypeHint is null)
                continue;

            var hint = schema.VendorTypeHint;

            if (!includeTypes.Contains(hint) && !includePatterns.Any(p => p.IsMatch(hint)))
                continue;

            if (excludeTypes.Contains(hint) || excludePatterns.Any(p => p.IsMatch(hint)))
                continue;

            var mapped = hint.Replace("+", ".");
            foreach (var (from, to) in config.TypeReuse.NamespaceMap)
            {
                if (mapped.StartsWith(from + ".", StringComparison.Ordinal))
                {
                    mapped = to + mapped[from.Length..];
                    break;
                }

                if (mapped == from)
                {
                    mapped = to;
                    break;
                }
            }

            schema.ExternalClrTypeName = mapped;

            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, DiagnosticCodes.TypeReused,
                $"Type '{hint}' reused as '{mapped}'. No code will be generated.",
                schema.Source));
        }

        return diagnostics;
    }

    private static List<Regex> BuildPatterns(List<string> globs) =>
        globs.Select(p => new Regex("^" + Regex.Escape(p).Replace("\\*", ".*") + "$", RegexOptions.CultureInvariant)).ToList();
}
