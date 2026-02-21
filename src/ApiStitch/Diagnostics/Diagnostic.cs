namespace ApiStitch.Diagnostics;

public record Diagnostic(DiagnosticSeverity Severity, string Code, string Message, string? SpecPath = null);
