using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Generation;

namespace ApiStitch.IntegrationTests;

public class RealSpecGenerationTests
{
    [Theory]
    [MemberData(nameof(RealSpecCorpus.DefaultEntries), MemberType = typeof(RealSpecCorpus))]
    public void LibraryPath_MatchesExpectedOutcome(RealSpecCorpusEntry entry)
    {
        var result = Generate(entry);
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

        switch (entry.ExpectedOutcome)
        {
            case RealSpecOutcome.Success:
                Assert.Empty(errors);
                Assert.Empty(warnings);
                Assert.NotEmpty(result.Files);
                break;
            case RealSpecOutcome.SuccessWithWarnings:
                Assert.Empty(errors);
                Assert.NotEmpty(result.Files);
                Assert.NotEmpty(warnings);
                AssertContainsExpectedDiagnostics(entry, warnings);
                break;
            case RealSpecOutcome.ExpectedDiagnosticFailure:
                Assert.NotEmpty(errors);
                AssertContainsExpectedDiagnostics(entry, errors);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [Theory]
    [MemberData(nameof(RealSpecCorpus.CompilableEntries), MemberType = typeof(RealSpecCorpus))]
    public void LibraryPath_SuccessfulEntries_Compile(RealSpecCorpusEntry entry)
    {
        var result = Generate(entry);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotEmpty(result.Files);

        var (success, compileDiags, _) = RoslynCompilationHelper.Compile(
            result.Files,
            assemblyName: $"RealSpec_{SanitizeIdentifier(entry.Id)}",
            excludeJsonContext: true);

        Assert.True(success, CompilationDiagnosticsFormatter.Format(compileDiags));
    }

    private static GenerationResult Generate(RealSpecCorpusEntry entry)
    {
        var config = new ApiStitchConfig
        {
            Spec = entry.ResolveFixturePath(),
            Namespace = $"Generated.RealSpecs.{SanitizeIdentifier(entry.Id)}",
            OutputStyle = OutputStyle.TypedClientStructured,
        };

        return new GenerationPipeline().Generate(config);
    }

    private static void AssertContainsExpectedDiagnostics(
        RealSpecCorpusEntry entry,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        foreach (var expectedCode in entry.ExpectedDiagnosticCodes)
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == expectedCode);

        foreach (var fragment in entry.ExpectedMessageFragments)
        {
            Assert.Contains(
                diagnostics,
                diagnostic => diagnostic.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string SanitizeIdentifier(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);

        foreach (var segment in value.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Append(char.ToUpperInvariant(segment[0]));

            for (var i = 1; i < segment.Length; i++)
            {
                if (char.IsLetterOrDigit(segment[i]))
                    builder.Append(segment[i]);
            }
        }

        return builder.Length == 0 ? "RealSpec" : builder.ToString();
    }
}
