using ApiStitch.Diagnostics;
using ApiStitch.Model;
using ApiStitch.Parsing;
using System.Net;
using System.Net.Http;

namespace ApiStitch.Tests.Parsing;

public class OpenApiSpecLoaderTests
{
    private static string SpecPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Parsing", "Specs", fileName);

    [Fact]
    public void ValidYaml_ReturnsDocument()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("minimal-valid.yaml"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(doc.Components?.Schemas);
        Assert.Single(doc.Components.Schemas);
    }

    [Fact]
    public void ValidJson_ReturnsDocument()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("minimal-valid.json"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(doc.Components?.Schemas);
        Assert.Single(doc.Components.Schemas);
    }

    [Fact]
    public void MissingFile_ReturnsError()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load("/nonexistent/spec.yaml");

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS100", diag.Code);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    [Fact]
    public void SwaggerV2_ParsedSuccessfully()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("swagger-v2.yaml"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SwaggerV2_WithSchemas_UpconvertedAndTransformable()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("swagger-v2-with-schemas.yaml"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(doc.Components?.Schemas);
        Assert.Contains(doc.Components.Schemas, s => s.Key == "Pet");

        var transformer = new SchemaTransformer();
        var (spec, _, transformDiags) = transformer.Transform(doc);
        Assert.DoesNotContain(transformDiags, d => d.Severity == DiagnosticSeverity.Error);

        var pet = Assert.Single(spec.Schemas);
        Assert.Equal("Pet", pet.Name);
        Assert.Equal(2, pet.Properties.Count);
    }

    [Fact]
    public void NoSchemas_ReturnsWarning()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("no-schemas.yaml"));

        Assert.NotNull(doc);
        Assert.Contains(diagnostics, d => d.Code == "AS103" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void OpenApi31_ParsedSuccessfully()
    {
        var (doc, diagnostics) = OpenApiSpecLoader.Load(SpecPath("minimal-valid-3.1.yaml"));

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InvalidContent_ReturnsError()
    {
        var tempFile = Path.GetTempFileName() + ".yaml";
        File.WriteAllText(tempFile, "this is not valid openapi content at all }{{}");
        try
        {
            var (doc, diagnostics) = OpenApiSpecLoader.Load(tempFile);

            Assert.True(
                doc == null || diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Should return null document or error diagnostics for invalid content");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RemoteHttpsYaml_ReturnsDocument()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: Remote
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    id:
                      type: integer
            """;

        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(yaml),
            }));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(doc.Components!.Schemas!, s => s.Key == "Pet");
    }

    [Fact]
    public async Task RemoteHttpJson_ReturnsDocument()
    {
        const string json = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Remote", "version": "1.0.0" },
              "paths": {},
              "components": {
                "schemas": {
                  "Pet": {
                    "type": "object",
                    "properties": {
                      "id": { "type": "integer" }
                    }
                  }
                }
              }
            }
            """;

        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            }));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("http://example.test/openapi.json", httpClient: client);

        Assert.NotNull(doc);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(doc.Components!.Schemas!, s => s.Key == "Pet");
    }

    [Fact]
    public async Task UnsupportedUriScheme_ReturnsError()
    {
        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("ftp://example.test/openapi.yaml");

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("Only http and https are supported", diag.Message);
    }

    [Fact]
    public async Task RemoteNonSuccessStatus_ReturnsError()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("HTTP 404", diag.Message);
    }

    [Fact]
    public async Task RemoteRequestException_ReturnsError()
    {
        using var client = new HttpClient(new StubHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new HttpRequestException("dns failure"))));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("dns failure", diag.Message);
    }

    [Fact]
    public async Task RemoteResponseTooLarge_ReturnsError()
    {
        var largePayload = new string('a', 10 * 1024 * 1024 + 1);
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(largePayload),
            }));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("maximum allowed size", diag.Message);
    }

    [Fact]
    public async Task RemoteRedirectLimitExceeded_ReturnsError()
    {
        var count = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            count++;
            return new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers =
                {
                    Location = new Uri($"https://example.test/redirect/{count}")
                }
            };
        }));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("redirect limit", diag.Message);
    }

    [Fact]
    public async Task RedirectToUnsupportedScheme_ReturnsError()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers =
                {
                    Location = new Uri("ftp://example.test/openapi.yaml")
                }
            }));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("Only http and https are supported", diag.Message);
    }

    [Fact]
    public async Task RemoteTimeout_ReturnsTimeoutDiagnostic()
    {
        using var client = new HttpClient(new StubHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new TaskCanceledException("request timeout"))));

        var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", httpClient: client);

        Assert.Null(doc);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS100", diag.Code);
        Assert.Contains("Timed out fetching OpenAPI spec URL", diag.Message);
    }

    [Fact]
    public async Task WindowsAbsolutePath_IsTreatedAsLocalPath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"apistitch-win-path-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(tempFile, """
            openapi: 3.0.3
            info:
              title: Local
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
            """);

        try
        {
            var (doc, diagnostics) = await OpenApiSpecLoader.LoadAsync(tempFile);

            Assert.NotNull(doc);
            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.DoesNotContain(diagnostics, d => d.Message.Contains("Only http and https are supported", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CancellationDuringRemoteFetch_ThrowsOperationCanceled()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            };
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await OpenApiSpecLoader.LoadAsync("https://example.test/openapi.yaml", cts.Token, client));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder = responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(request => Task.FromResult(responder(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responder(request);
        }
    }
}
