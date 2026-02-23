namespace ApiStitch.Model;

/// <summary>
/// HTTP method used by an API operation.
/// </summary>
public enum ApiHttpMethod
{
    /// <summary>HTTP GET.</summary>
    Get,
    /// <summary>HTTP POST.</summary>
    Post,
    /// <summary>HTTP PUT.</summary>
    Put,
    /// <summary>HTTP DELETE.</summary>
    Delete,
    /// <summary>HTTP PATCH.</summary>
    Patch,
    /// <summary>HTTP HEAD.</summary>
    Head,
    /// <summary>HTTP OPTIONS.</summary>
    Options
}
