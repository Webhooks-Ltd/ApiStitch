using ApiStitch.Configuration;
using ApiStitch.Diagnostics;

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

        var (config, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

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

        var (config, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("./petstore.yaml", config.Spec);
        Assert.Equal("ApiStitch.Generated", config.Namespace);
        Assert.Equal("./Generated", config.OutputDir);
    }

    [Fact]
    public void MissingSpec_ReturnsError()
    {
        var yaml = "namespace: MyApi.Models";

        var (config, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

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

        var (config, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS302", diag.Code);
    }

    [Fact]
    public void InvalidYaml_ReturnsError()
    {
        var yaml = ": : : not valid yaml [[[";

        var (config, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

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

        var (config, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("./petstore.yaml", config.Spec);
    }

    [Fact]
    public void FileNotFound_ReturnsError()
    {
        var (config, diagnostics) = ConfigLoader.Load("/nonexistent/path/apistitch.yaml");

        Assert.Null(config);
        var diag = Assert.Single(diagnostics);
        Assert.Equal("AS300", diag.Code);
        Assert.Contains("/nonexistent/path/apistitch.yaml", diag.Message);
    }
}
