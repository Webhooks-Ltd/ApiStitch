using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ApiStitch.Parsing;

/// <summary>
/// Builds a .NET project and extracts its OpenAPI spec using dotnet-getdocument.
/// </summary>
public static class ProjectSpecExtractor
{
    /// <summary>
    /// Builds the project and extracts the OpenAPI spec to a temporary file.
    /// Returns the path to the extracted spec file.
    /// </summary>
    public static async Task<(string? SpecPath, string? Error)> ExtractAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        projectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(projectPath))
            return (null, $"Project file not found: {projectPath}");

        var projectDir = Path.GetDirectoryName(projectPath)!;

        var buildResult = await RunAsync("dotnet", $"build \"{projectPath}\" -c Release --nologo -v q", projectDir, cancellationToken);
        if (buildResult.ExitCode != 0)
            return (null, $"Failed to build project: {buildResult.StdErr.Trim()}");

        var propsResult = await RunAsync("dotnet", $"msbuild \"{projectPath}\" -getProperty:TargetPath,ProjectAssetsFile,TargetFrameworkMoniker -nologo", projectDir, cancellationToken);
        if (propsResult.ExitCode != 0)
            return (null, $"Failed to read project properties: {propsResult.StdErr.Trim()}");

        string targetPath, assetsFile, tfm;
        try
        {
            var json = JsonDocument.Parse(propsResult.StdOut);
            var props = json.RootElement.GetProperty("Properties");
            targetPath = props.GetProperty("TargetPath").GetString()!;
            assetsFile = props.GetProperty("ProjectAssetsFile").GetString()!;
            tfm = props.GetProperty("TargetFrameworkMoniker").GetString()!;
        }
        catch (Exception ex)
        {
            return (null, $"Failed to parse project properties: {ex.Message}");
        }

        var getDocTool = FindGetDocumentTool(assetsFile);
        if (getDocTool is null)
            return (null, "Could not find dotnet-getdocument tool. Ensure the project references Microsoft.Extensions.ApiDescription.Server.");

        var outputDir = Path.Combine(Path.GetTempPath(), "apistitch-spec-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDir);

        var fileList = Path.Combine(outputDir, "filelist.cache");
        var projectName = Path.GetFileNameWithoutExtension(projectPath);

        var args = new StringBuilder();
        args.Append($"\"{getDocTool}\"");
        args.Append($" --assembly \"{targetPath}\"");
        args.Append($" --file-list \"{fileList}\"");
        args.Append($" --framework \"{tfm}\"");
        args.Append($" --output \"{outputDir}\"");
        args.Append($" --project \"{projectName}\"");
        args.Append($" --assets-file \"{assetsFile}\"");

        var extractResult = await RunAsync("dotnet", args.ToString(), projectDir, cancellationToken);
        if (extractResult.ExitCode != 0)
        {
            var detail = !string.IsNullOrWhiteSpace(extractResult.StdErr)
                ? extractResult.StdErr.Trim()
                : extractResult.StdOut.Trim();
            return (null, $"Failed to extract OpenAPI spec (exit code {extractResult.ExitCode}): {detail}");
        }

        if (!File.Exists(fileList))
            return (null, "OpenAPI spec extraction produced no output. Ensure the project has OpenAPI document generation enabled.");

        var files = File.ReadAllLines(fileList).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        if (files.Length == 0)
            return (null, "OpenAPI spec extraction produced no files.");

        return (files[0], null);
    }

    private static string? FindGetDocumentTool(string assetsFile)
    {
        if (!File.Exists(assetsFile))
            return null;

        try
        {
            var json = JsonDocument.Parse(File.ReadAllText(assetsFile));
            var libraries = json.RootElement.GetProperty("libraries");

            foreach (var lib in libraries.EnumerateObject())
            {
                if (lib.Name.StartsWith("Microsoft.Extensions.ApiDescription.Server/", StringComparison.OrdinalIgnoreCase))
                {
                    var version = lib.Name.Split('/')[1];
                    var nugetDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".nuget", "packages",
                        "microsoft.extensions.apidescription.server",
                        version,
                        "tools", "dotnet-getdocument.dll");

                    if (File.Exists(nugetDir))
                        return nugetDir;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, stdout, stderr);
    }
}
