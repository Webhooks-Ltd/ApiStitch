namespace ApiStitch.Parsing;

internal enum SpecSourceKind
{
    LocalPath,
    RemoteHttp,
    UnsupportedUri,
}

internal static class SpecSourceClassifier
{
    internal static SpecSourceKind Classify(string specInput)
    {
        if (string.IsNullOrWhiteSpace(specInput))
            return SpecSourceKind.LocalPath;

        if (Uri.TryCreate(specInput, UriKind.Absolute, out var absolute))
        {
            if (absolute.IsFile)
                return SpecSourceKind.LocalPath;

            if (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                return SpecSourceKind.RemoteHttp;

            if (specInput.Contains("://", StringComparison.Ordinal))
                return SpecSourceKind.UnsupportedUri;
        }

        return SpecSourceKind.LocalPath;
    }
}
