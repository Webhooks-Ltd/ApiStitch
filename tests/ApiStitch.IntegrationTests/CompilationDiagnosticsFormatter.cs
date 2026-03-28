namespace ApiStitch.IntegrationTests;

internal static class CompilationDiagnosticsFormatter
{
    public static string Format(IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        var relevantDiagnostics = diagnostics
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();

        if (relevantDiagnostics.Count == 0)
            relevantDiagnostics = diagnostics.ToList();

        return $"Compilation failed:\n{string.Join("\n", relevantDiagnostics.Select(d => $"  {d.Location}: {d.GetMessage()}"))}";
    }
}
