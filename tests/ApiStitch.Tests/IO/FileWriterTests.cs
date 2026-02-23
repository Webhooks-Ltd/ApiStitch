using System.Text;
using ApiStitch.Generation;
using ApiStitch.IO;

namespace ApiStitch.Tests.IO;

public class FileWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "apistitch-test-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WriteToEmptyDirectory_AllFilesWritten()
    {
        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "public class Pet { }"),
            new("Category.cs", "public class Category { }"),
            new("Tag.cs", "public class Tag { }"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir);

        Assert.Equal(3, result.Written.Count);
        Assert.Empty(result.Unchanged);
        Assert.Empty(result.Deleted);
        Assert.True(File.Exists(Path.Combine(_tempDir, "Pet.cs")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Category.cs")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Tag.cs")));
    }

    [Fact]
    public async Task WriteToExistingDirectory_UnchangedAndWritten()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Pet.cs"), "public class Pet { }");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Category.cs"), "public class Category { // old }");

        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "public class Pet { }"),
            new("Category.cs", "public class Category { }"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir);

        Assert.Single(result.Written);
        Assert.Contains("Category.cs", result.Written);
        Assert.Single(result.Unchanged);
        Assert.Contains("Pet.cs", result.Unchanged);
    }

    [Fact]
    public async Task NestedRelativePath_CreatesSubdirectories()
    {
        var files = new List<GeneratedFile>
        {
            new("Models/Pet.cs", "public class Pet { }"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir);

        Assert.Single(result.Written);
        Assert.True(File.Exists(Path.Combine(_tempDir, "Models", "Pet.cs")));
    }

    [Fact]
    public async Task FilesEncodedAsUtf8WithoutBom()
    {
        var files = new List<GeneratedFile>
        {
            new("Test.cs", "public class Test { }"),
        };

        await FileWriter.WriteAsync(files, _tempDir);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "Test.cs"));
        Assert.NotEqual((byte)0xEF, bytes[0]);
    }

    [Fact]
    public async Task PathTraversal_ThrowsArgumentException()
    {
        var files = new List<GeneratedFile>
        {
            new("../../etc/passwd", "malicious"),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => FileWriter.WriteAsync(files, _tempDir));
    }

    [Fact]
    public async Task AbsolutePath_ThrowsArgumentException()
    {
        var files = new List<GeneratedFile>
        {
            new("/etc/passwd", "malicious"),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => FileWriter.WriteAsync(files, _tempDir));
    }

    [Fact]
    public async Task EmptyFileList_ReturnsEmptyResult()
    {
        var result = await FileWriter.WriteAsync([], _tempDir);

        Assert.Empty(result.Written);
        Assert.Empty(result.Unchanged);
        Assert.Empty(result.Deleted);
    }

    [Fact]
    public async Task ContentUnchanged_PreservesTimestamp()
    {
        Directory.CreateDirectory(_tempDir);
        var filePath = Path.Combine(_tempDir, "Pet.cs");
        await File.WriteAllTextAsync(filePath, "public class Pet { }");
        var originalTime = File.GetLastWriteTimeUtc(filePath);

        await Task.Delay(100);

        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "public class Pet { }"),
        };

        await FileWriter.WriteAsync(files, _tempDir);

        var newTime = File.GetLastWriteTimeUtc(filePath);
        Assert.Equal(originalTime, newTime);
    }

    [Fact]
    public async Task LineEndingDifference_TriggersRewrite()
    {
        Directory.CreateDirectory(_tempDir);
        var filePath = Path.Combine(_tempDir, "Pet.cs");
        await File.WriteAllTextAsync(filePath, "line1\r\nline2\r\n");

        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "line1\nline2\n"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir);

        Assert.Single(result.Written);
        Assert.Contains("Pet.cs", result.Written);
    }

    [Fact]
    public async Task ManifestWrittenOnFirstRun_CleanOutputTrue()
    {
        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "public class Pet { }"),
            new("Category.cs", "public class Category { }"),
        };

        await FileWriter.WriteAsync(files, _tempDir, new FileWriteOptions { CleanOutput = true });

        var manifestPath = Path.Combine(_tempDir, ".apistitch.manifest");
        Assert.True(File.Exists(manifestPath));
        var lines = await File.ReadAllLinesAsync(manifestPath);
        Assert.Equal(["Category.cs", "Pet.cs"], lines);
    }

    [Fact]
    public async Task StaleFilesDeleted_CleanOutputTrue()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Pet.cs"), "old");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Category.cs"), "old");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Dog.cs"), "old");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".apistitch.manifest"), "Category.cs\nDog.cs\nPet.cs\n");

        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "new"),
            new("Animal.cs", "new"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir, new FileWriteOptions { CleanOutput = true });

        Assert.Contains("Category.cs", result.Deleted);
        Assert.Contains("Dog.cs", result.Deleted);
        Assert.False(File.Exists(Path.Combine(_tempDir, "Category.cs")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "Dog.cs")));
    }

    [Fact]
    public async Task StaleFileAlreadyDeleted_NoError()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Pet.cs"), "old");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".apistitch.manifest"), "Pet.cs\nGone.cs\n");

        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "new"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir, new FileWriteOptions { CleanOutput = true });

        Assert.DoesNotContain("Gone.cs", result.Deleted);
    }

    [Fact]
    public async Task CleanOutputFalse_NoManifestNoDeletes()
    {
        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "public class Pet { }"),
        };

        await FileWriter.WriteAsync(files, _tempDir);

        Assert.False(File.Exists(Path.Combine(_tempDir, ".apistitch.manifest")));
    }

    [Fact]
    public async Task EmptyFileList_CleanOutputTrue_NoDeletions()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Pet.cs"), "old");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".apistitch.manifest"), "Pet.cs\n");

        var result = await FileWriter.WriteAsync([], _tempDir, new FileWriteOptions { CleanOutput = true });

        Assert.Empty(result.Deleted);
        Assert.True(File.Exists(Path.Combine(_tempDir, "Pet.cs")));
    }

    [Fact]
    public async Task ManifestExcludedFromResults()
    {
        var files = new List<GeneratedFile>
        {
            new("Pet.cs", "public class Pet { }"),
        };

        var result = await FileWriter.WriteAsync(files, _tempDir, new FileWriteOptions { CleanOutput = true });

        Assert.DoesNotContain(".apistitch.manifest", result.Written);
        Assert.DoesNotContain(".apistitch.manifest", result.Unchanged);
        Assert.DoesNotContain(".apistitch.manifest", result.Deleted);
    }

    [Fact]
    public async Task CancellationDuringWrite_ThrowsAndLeavesWrittenFiles()
    {
        var cts = new CancellationTokenSource();
        var files = new List<GeneratedFile>
        {
            new("First.cs", "first"),
            new("Second.cs", "second"),
        };

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => FileWriter.WriteAsync(files, _tempDir, cancellationToken: cts.Token));
    }
}
