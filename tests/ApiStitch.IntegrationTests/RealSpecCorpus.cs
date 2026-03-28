using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiStitch.IntegrationTests;

public enum RealSpecOutcome
{
    Success,
    SuccessWithWarnings,
    ExpectedDiagnosticFailure,
}

public sealed record RealSpecCorpusEntry(
    string Id,
    string DisplayName,
    string FixturePath,
    string SourceName,
    string SourceUrl,
    string PinnedRevision,
    RealSpecOutcome ExpectedOutcome,
    IReadOnlyList<string> ExpectedDiagnosticCodes,
    IReadOnlyList<string> ExpectedMessageFragments,
    int CliTimeoutMs,
    bool IsStress)
{
    public string ResolveFixturePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "RealSpecs",
            FixturePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public override string ToString() => Id;
}

public static class RealSpecCorpus
{
    private static readonly Lazy<IReadOnlyList<RealSpecCorpusEntry>> Entries = new(LoadEntries);

    public static IEnumerable<object[]> DefaultEntries()
    {
        return Entries.Value
            .Where(entry => !entry.IsStress)
            .Select(entry => new object[] { entry });
    }

    public static IEnumerable<object[]> CompilableEntries()
    {
        return Entries.Value
            .Where(entry => !entry.IsStress && entry.ExpectedOutcome != RealSpecOutcome.ExpectedDiagnosticFailure)
            .Select(entry => new object[] { entry });
    }

    public static IEnumerable<object[]> StressEntries()
    {
        return Entries.Value
            .Where(entry => entry.IsStress)
            .Select(entry => new object[] { entry });
    }

    private static IReadOnlyList<RealSpecCorpusEntry> LoadEntries()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "RealSpecs", "manifest.json");
        var manifestJson = File.ReadAllText(manifestPath);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        var manifest = JsonSerializer.Deserialize<RealSpecManifest>(manifestJson, options)
            ?? throw new InvalidOperationException($"Failed to deserialize manifest '{manifestPath}'.");

        var entries = manifest.Entries
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(entry => entry.ToCorpusEntry())
            .ToList();

        var duplicateIds = entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        Assert.True(duplicateIds.Count == 0, $"Duplicate real-spec corpus ids: {string.Join(", ", duplicateIds)}");

        foreach (var entry in entries)
            Assert.True(File.Exists(entry.ResolveFixturePath()), $"Missing real-spec fixture '{entry.ResolveFixturePath()}'.");

        return entries;
    }

    private sealed record RealSpecManifest(IReadOnlyList<RealSpecManifestEntry> Entries);

    private sealed record RealSpecManifestEntry(
        string Id,
        string DisplayName,
        string FixturePath,
        string SourceName,
        string SourceUrl,
        string PinnedRevision,
        RealSpecOutcome ExpectedOutcome,
        IReadOnlyList<string>? ExpectedDiagnosticCodes,
        IReadOnlyList<string>? ExpectedMessageFragments,
        int? CliTimeoutMs,
        bool? IsStress)
    {
        public RealSpecCorpusEntry ToCorpusEntry()
        {
            return new RealSpecCorpusEntry(
                Id,
                DisplayName,
                FixturePath,
                SourceName,
                SourceUrl,
                PinnedRevision,
                ExpectedOutcome,
                ExpectedDiagnosticCodes ?? [],
                ExpectedMessageFragments ?? [],
                CliTimeoutMs ?? 120_000,
                IsStress ?? false);
        }
    }
}
