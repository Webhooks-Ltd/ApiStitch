using System.Net;
using ApiStitch.Diagnostics;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace ApiStitch.Parsing;

public static class OpenApiSpecLoader
{
    private const string SpecReadCode = "AS100";
    private const int MaxRedirects = 5;
    private const int MaxPayloadBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly HttpClient SharedHttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });

    public static (OpenApiDocument? Document, IReadOnlyList<Diagnostic> Diagnostics) Load(string specInput)
    {
        return LoadAsync(specInput, CancellationToken.None).GetAwaiter().GetResult();
    }

    public static async Task<(OpenApiDocument? Document, IReadOnlyList<Diagnostic> Diagnostics)> LoadAsync(
        string specInput,
        CancellationToken cancellationToken = default,
        HttpClient? httpClient = null)
    {
        var sourceKind = SpecSourceClassifier.Classify(specInput);
        if (sourceKind == SpecSourceKind.UnsupportedUri)
        {
            return (null,
            [
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    SpecReadCode,
                    $"Unsupported OpenAPI spec URI scheme. Only http and https are supported: {specInput}",
                    specInput)
            ]);
        }

        string content;
        string sourcePath;

        if (sourceKind == SpecSourceKind.RemoteHttp)
        {
            var remoteResult = await LoadRemoteContentAsync(specInput, httpClient ?? SharedHttpClient, cancellationToken).ConfigureAwait(false);
            if (remoteResult.Diagnostics.Count > 0)
                return (null, remoteResult.Diagnostics);

            content = remoteResult.Content!;
            sourcePath = specInput;
        }
        else
        {
            sourcePath = ResolveLocalPath(specInput);
            if (!File.Exists(sourcePath))
            {
                return (null,
                [
                    new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"OpenAPI spec file not found: {sourcePath}", sourcePath)
                ]);
            }

            try
            {
                content = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return (null,
                [
                    new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Failed to read OpenAPI spec: {ex.Message}", sourcePath)
                ]);
            }
        }

        return ParseContent(content, sourcePath);
    }

    private static (OpenApiDocument? Document, IReadOnlyList<Diagnostic> Diagnostics) ParseContent(string content, string sourcePath)
    {
        try
        {
            var settings = new OpenApiReaderSettings();
            settings.AddYamlReader();
            var result = OpenApiDocument.Parse(content, settings: settings);

            var diagnostics = new List<Diagnostic>();
            var document = result.Document;

            foreach (var error in result.Diagnostic?.Errors ?? [])
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    SpecReadCode,
                    error.Message,
                    error.Pointer));
            }

            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                return (null, diagnostics);

            foreach (var warning in result.Diagnostic?.Warnings ?? [])
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    SpecReadCode,
                    warning.Message,
                    warning.Pointer));
            }

            if (document?.Components?.Schemas == null || document.Components.Schemas.Count == 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    "AS103",
                    "OpenAPI spec has no component schemas. No types will be generated.",
                    sourcePath));
            }

            return (document, diagnostics);
        }
        catch (Exception ex)
        {
            return (null,
            [
                new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Failed to read OpenAPI spec: {ex.Message}", sourcePath)
            ]);
        }
    }

    private static async Task<(string? Content, IReadOnlyList<Diagnostic> Diagnostics)> LoadRemoteContentAsync(
        string specInput,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(specInput, UriKind.Absolute, out var currentUri))
        {
            return (null,
            [
                new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Invalid OpenAPI spec URL: {specInput}", specInput)
            ]);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);
        var token = timeoutCts.Token;

        var redirects = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

                if (IsRedirectStatus(response.StatusCode))
                {
                    if (response.Headers.Location is null)
                    {
                        return (null,
                        [
                            new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Remote spec URL redirect missing Location header: {currentUri}", currentUri.ToString())
                        ]);
                    }

                    var nextUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);

                    if (nextUri.Scheme != Uri.UriSchemeHttp && nextUri.Scheme != Uri.UriSchemeHttps)
                    {
                        return (null,
                        [
                            new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Unsupported OpenAPI spec URI scheme. Only http and https are supported: {nextUri}", nextUri.ToString())
                        ]);
                    }

                    redirects++;
                    if (redirects > MaxRedirects)
                    {
                        return (null,
                        [
                            new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Remote spec URL exceeded redirect limit ({MaxRedirects}): {specInput}", specInput)
                        ]);
                    }

                    currentUri = nextUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return (null,
                    [
                        new Diagnostic(
                            DiagnosticSeverity.Error,
                            SpecReadCode,
                            $"Failed to fetch OpenAPI spec URL '{specInput}': HTTP {(int)response.StatusCode} {response.StatusCode}",
                            specInput)
                    ]);
                }

                var content = await ReadResponseContentWithLimitAsync(response, token).ConfigureAwait(false);
                return (content, []);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (null,
                [
                    new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Timed out fetching OpenAPI spec URL after {RequestTimeout.TotalSeconds:0} seconds: {specInput}", specInput)
                ]);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ResponseTooLargeException ex)
            {
                return (null,
                [
                    new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, ex.Message, specInput)
                ]);
            }
            catch (Exception ex)
            {
                return (null,
                [
                    new Diagnostic(DiagnosticSeverity.Error, SpecReadCode, $"Failed to fetch OpenAPI spec URL '{specInput}': {ex.Message}", specInput)
                ]);
            }
        }
    }

    private static async Task<string> ReadResponseContentWithLimitAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        var totalRead = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            totalRead += read;
            if (totalRead > MaxPayloadBytes)
                throw new ResponseTooLargeException($"OpenAPI spec response exceeds maximum allowed size ({MaxPayloadBytes} bytes).");

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    private static bool IsRedirectStatus(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.Moved
            || statusCode == HttpStatusCode.Redirect
            || statusCode == HttpStatusCode.RedirectMethod
            || statusCode == HttpStatusCode.RedirectKeepVerb
            || statusCode == HttpStatusCode.TemporaryRedirect
            || (int)statusCode == 308;
    }

    private static string ResolveLocalPath(string specInput)
    {
        if (Uri.TryCreate(specInput, UriKind.Absolute, out var absolute) && absolute.IsFile)
            return absolute.LocalPath;

        return specInput;
    }

    private sealed class ResponseTooLargeException(string message) : Exception(message);
}
