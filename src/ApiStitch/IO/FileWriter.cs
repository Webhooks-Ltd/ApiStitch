using System.Text;
using ApiStitch.Generation;

namespace ApiStitch.IO;

/// <summary>
/// Writes generated files to disk with content-comparison skip and optional manifest-based stale cleanup.
/// </summary>
public static class FileWriter
{
    private const string ManifestFileName = ".apistitch.manifest";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes generated files to the specified output directory.
    /// </summary>
    public static async Task<FileWriteResult> WriteAsync(
        IReadOnlyList<GeneratedFile> files,
        string outputDirectory,
        FileWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            return new FileWriteResult([], [], []);

        var cleanOutput = options?.CleanOutput ?? false;

        foreach (var file in files)
        {
            ValidatePath(file.RelativePath);
        }

        Directory.CreateDirectory(outputDirectory);

        HashSet<string>? previousManifest = null;
        if (cleanOutput)
        {
            previousManifest = ReadManifest(outputDirectory);
        }

        var written = new List<string>();
        var unchanged = new List<string>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(outputDirectory, file.RelativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
                Directory.CreateDirectory(directory);

            if (File.Exists(fullPath))
            {
                var existing = await File.ReadAllTextAsync(fullPath, Utf8NoBom, cancellationToken);
                if (existing == file.Content)
                {
                    unchanged.Add(file.RelativePath);
                    continue;
                }
            }

            await File.WriteAllTextAsync(fullPath, file.Content, Utf8NoBom, cancellationToken);
            written.Add(file.RelativePath);
        }

        var deleted = new List<string>();

        if (cleanOutput)
        {
            var currentFiles = new HashSet<string>(files.Select(f => f.RelativePath), StringComparer.OrdinalIgnoreCase);
            WriteManifest(outputDirectory, currentFiles);

            if (previousManifest != null)
            {
                foreach (var stale in previousManifest.Except(currentFiles, StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var stalePath = Path.Combine(outputDirectory, stale);
                    if (File.Exists(stalePath))
                    {
                        File.Delete(stalePath);
                        deleted.Add(stale);
                    }
                }
            }
        }

        return new FileWriteResult(written, unchanged, deleted);
    }

    private static void ValidatePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException($"RelativePath must not be absolute: {relativePath}", nameof(relativePath));

        if (relativePath.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException($"RelativePath must not contain '..' segments: {relativePath}", nameof(relativePath));
    }

    private static HashSet<string> ReadManifest(string outputDirectory)
    {
        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
            return [];

        var lines = File.ReadAllLines(manifestPath, Utf8NoBom);
        return new HashSet<string>(lines.Where(l => !string.IsNullOrWhiteSpace(l)), StringComparer.OrdinalIgnoreCase);
    }

    private static void WriteManifest(string outputDirectory, HashSet<string> currentFiles)
    {
        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        var sorted = currentFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        File.WriteAllLines(manifestPath, sorted, Utf8NoBom);
    }
}
