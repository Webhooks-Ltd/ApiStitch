using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiStitch.OpenApi.Tests;

public class SchemaTransformerIntegrationTests
{
    private static async Task<(IHost Host, HttpClient Client)> CreateTestApp(Action<ApiStitchTypeInfoOptions>? configure = null)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    if (configure is not null)
                        services.AddOpenApi(options => options.AddApiStitchTypeInfo(configure));
                    else
                        services.AddOpenApi(options => options.AddApiStitchTypeInfo());
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapOpenApi();
                        endpoints.MapGet("/pets", () => new List<TestPet>
                        {
                            new() { Id = 1, Name = "Fido", Status = TestPetStatus.Available }
                        });
                        endpoints.MapPost("/pets", (TestCreatePetRequest request) =>
                            new TestPet { Id = 2, Name = request.Name, Status = TestPetStatus.Available });
                    });
                });
            });

        var host = await builder.StartAsync();
        var client = host.GetTestClient();
        return (host, client);
    }

    [Fact]
    public async Task AlwaysEmitTrue_ExtensionsAppearOnUserDefinedSchemas()
    {
        var (host, client) = await CreateTestApp(o => o.AlwaysEmit = true);
        try
        {
            var response = await client.GetAsync("/openapi/v1.json");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");

            schemas.GetProperty("TestPet").TryGetProperty("x-apistitch-type", out var petExt).Should().BeTrue();
            petExt.GetString().Should().Be("ApiStitch.OpenApi.Tests.TestPet");

            schemas.GetProperty("TestPetStatus").TryGetProperty("x-apistitch-type", out var statusExt).Should().BeTrue();
            statusExt.GetString().Should().Be("ApiStitch.OpenApi.Tests.TestPetStatus");

            schemas.GetProperty("TestCreatePetRequest").TryGetProperty("x-apistitch-type", out var createExt).Should().BeTrue();
            createExt.GetString().Should().Be("ApiStitch.OpenApi.Tests.TestCreatePetRequest");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
            client.Dispose();
        }
    }

    [Fact]
    public async Task DefaultOptions_NoExtensionsAtRuntime()
    {
        var (host, client) = await CreateTestApp();
        try
        {
            var response = await client.GetAsync("/openapi/v1.json");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotContain("x-apistitch-type");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
            client.Dispose();
        }
    }
}

public class TestPet
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public TestPetStatus Status { get; set; }
}

public enum TestPetStatus
{
    Available,
    Pending,
    Adopted,
}

public class TestCreatePetRequest
{
    public required string Name { get; init; }
}
