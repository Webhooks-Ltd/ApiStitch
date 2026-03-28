namespace ApiStitch.IntegrationTests;

public class RealSpecCliTests
{
    [Theory]
    [MemberData(nameof(RealSpecCorpus.DefaultEntries), MemberType = typeof(RealSpecCorpus))]
    public async Task CliPath_MatchesExpectedOutcome(RealSpecCorpusEntry entry)
    {
        var outputDir = Path.Combine(
            Path.GetTempPath(),
            $"apistitch-real-spec-{entry.Id}-{Guid.NewGuid():N}");

        try
        {
            var result = await CliTestHelper.RunAsync(
                $"generate --spec \"{entry.ResolveFixturePath()}\" --output \"{outputDir}\"",
                timeoutMs: entry.CliTimeoutMs);

            Assert.DoesNotContain("   at ", result.Stderr, StringComparison.Ordinal);

            switch (entry.ExpectedOutcome)
            {
                case RealSpecOutcome.Success:
                    Assert.Equal(0, result.ExitCode);
                    Assert.True(Directory.Exists(outputDir));
                    Assert.NotEmpty(Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories));
                    break;
                case RealSpecOutcome.SuccessWithWarnings:
                    Assert.Equal(0, result.ExitCode);
                    Assert.True(Directory.Exists(outputDir));
                    Assert.NotEmpty(Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories));
                    AssertContainsExpectedDiagnostics(entry, result.Stderr);
                    break;
                case RealSpecOutcome.ExpectedDiagnosticFailure:
                    Assert.Equal(1, result.ExitCode);
                    AssertContainsExpectedDiagnostics(entry, result.Stderr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    private static void AssertContainsExpectedDiagnostics(RealSpecCorpusEntry entry, string stderr)
    {
        foreach (var expectedCode in entry.ExpectedDiagnosticCodes)
            Assert.Contains(expectedCode, stderr, StringComparison.Ordinal);

        foreach (var fragment in entry.ExpectedMessageFragments)
            Assert.Contains(fragment, stderr, StringComparison.OrdinalIgnoreCase);
    }
}
