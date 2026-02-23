namespace ApiStitch.IO;

/// <summary>
/// Reports the outcome of a file write operation.
/// </summary>
public sealed record FileWriteResult(
    IReadOnlyList<string> Written,
    IReadOnlyList<string> Unchanged,
    IReadOnlyList<string> Deleted);
