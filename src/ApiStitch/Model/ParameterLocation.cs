namespace ApiStitch.Model;

/// <summary>
/// Location of an API parameter within the HTTP request.
/// </summary>
public enum ParameterLocation
{
    /// <summary>Path segment parameter (e.g. /pets/{petId}).</summary>
    Path,
    /// <summary>Query string parameter.</summary>
    Query,
    /// <summary>HTTP header parameter.</summary>
    Header
}
