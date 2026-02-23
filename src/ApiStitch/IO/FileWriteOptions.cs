namespace ApiStitch.IO;

/// <summary>
/// Options controlling file write behaviour.
/// </summary>
public sealed class FileWriteOptions
{
    /// <summary>
    /// When true, maintains a manifest and deletes stale files from previous runs.
    /// </summary>
    public bool CleanOutput { get; init; }
}
