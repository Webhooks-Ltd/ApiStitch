using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.IO;

namespace ApiStitch.Tests.Configuration;

public class ConfigLoaderTests
{
    [Fact]
    public void ValidConfig_AllProperties()
    {
        var yaml = """
            spec: ./petstore.yaml
            namespace: MyApi.Models
            outputDir: ./Output
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("./petstore.yaml", config.Spec);
        Assert.Equal("MyApi.Models", config.Namespace);
        Assert.Equal("./Output", config.OutputDir);
    }

    [Fact]
    public void ValidConfig_DefaultsApplied()
    {
        var yaml = "spec: ./petstore.yaml";

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("./petstore.yaml", config.Spec);
        Assert.Equal("ApiStitch.Generated", config.Namespace);
        Assert.Equal("./Generated", config.OutputDir);
        Assert.Equal(OutputStyle.TypedClient, config.OutputStyle);
        Assert.Null(config.ClientName);
    }

    [Fact]
    public void MissingSpec_ReturnsError()
    {
        var yaml = "namespace: MyApi.Models";

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS302", diag.Code);
    }

    [Fact]
    public void EmptySpec_ReturnsError()
    {
        var yaml = """
            spec: ""
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS302", diag.Code);
    }

    [Fact]
    public void InvalidYaml_ReturnsError()
    {
        var yaml = ": : : not valid yaml [[[";

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS301", diag.Code);
    }

    [Fact]
    public void UnknownProperties_Ignored()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeMappings:
              Foo: Bar
            unknownThing: hello
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("./petstore.yaml", config.Spec);
    }

    [Fact]
    public void FileNotFound_ReturnsError()
    {
        var (config, _, diagnostics) = ConfigLoader.Load("/nonexistent/path/apistitch.yaml");

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS300", diag.Code);
        Assert.Contains("/nonexistent/path/apistitch.yaml", diag.Message);
    }

    [Fact]
    public void OutputStyle_ExplicitTypedClient()
    {
        var yaml = """
            spec: ./petstore.yaml
            outputStyle: TypedClient
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal(OutputStyle.TypedClient, config.OutputStyle);
    }

    [Fact]
    public void OutputStyle_CaseInsensitive()
    {
        var yaml = """
            spec: ./petstore.yaml
            outputStyle: typedclient
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal(OutputStyle.TypedClient, config.OutputStyle);
    }

    [Fact]
    public void OutputStyle_UnknownValue_ReturnsError()
    {
        var yaml = """
            spec: ./petstore.yaml
            outputStyle: Refit
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("AS303", diag.Code);
        Assert.Contains("Refit", diag.Message);
    }

    [Fact]
    public void ClientName_ExplicitValue()
    {
        var yaml = """
            spec: ./petstore.yaml
            clientName: MyPetApi
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("MyPetApi", config.ClientName);
    }

    [Fact]
    public void ClientName_WhitespaceOnly_TreatedAsNull()
    {
        var yaml = """
            spec: ./petstore.yaml
            clientName: "  "
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Null(config.ClientName);
    }

    [Fact]
    public void Delivery_CleanOutputTrue()
    {
        var yaml = """
            spec: ./petstore.yaml
            delivery:
              cleanOutput: true
            """;

        var (config, delivery, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.True(delivery.CleanOutput);
    }

    [Fact]
    public void Delivery_AbsentSection_Defaults()
    {
        var yaml = "spec: ./petstore.yaml";

        var (config, delivery, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.False(delivery.CleanOutput);
    }

    [Fact]
    public void Delivery_UnknownProperties_Ignored()
    {
        var yaml = """
            spec: ./petstore.yaml
            delivery:
              cleanOutput: true
              futureProperty: foo
            """;

        var (config, delivery, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.True(delivery.CleanOutput);
    }
}
