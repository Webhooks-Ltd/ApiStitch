namespace ApiStitch.Model;

/// <summary>
/// Discriminates the content type category for request and response bodies.
/// </summary>
public enum ContentKind
{
    Json,
    FormUrlEncoded,
    MultipartFormData,
    OctetStream,
    PlainText,
}
