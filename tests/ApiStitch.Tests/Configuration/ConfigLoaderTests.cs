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

    [Fact]
    public void TypeReuse_FullSection()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeReuse:
              includeNamespaces:
                - "SomeLib.*"
              includeTypes:
                - "OtherLib.SpecificType"
              excludeNamespaces:
                - "Microsoft.AspNetCore.*"
                - "System.*"
              excludeTypes:
                - "SomeLib.SomeType"
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal(["SomeLib.*"], config.TypeReuse.IncludeNamespaces);
        Assert.Equal(["OtherLib.SpecificType"], config.TypeReuse.IncludeTypes);
        Assert.Equal(["Microsoft.AspNetCore.*", "System.*"], config.TypeReuse.ExcludeNamespaces);
        Assert.Equal(["SomeLib.SomeType"], config.TypeReuse.ExcludeTypes);
    }

    [Fact]
    public void TypeReuse_AbsentSection_DefaultsToEmpty()
    {
        var yaml = "spec: ./petstore.yaml";

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Empty(config.TypeReuse.ExcludeNamespaces);
        Assert.Empty(config.TypeReuse.ExcludeTypes);
    }

    [Fact]
    public void TypeReuse_EmptySection_DefaultsToEmpty()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeReuse:
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Empty(config.TypeReuse.ExcludeNamespaces);
        Assert.Empty(config.TypeReuse.ExcludeTypes);
    }

    [Fact]
    public void TypeReuse_OnlyExcludeNamespaces()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeReuse:
              excludeNamespaces:
                - "Microsoft.*"
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal(["Microsoft.*"], config.TypeReuse.ExcludeNamespaces);
        Assert.Empty(config.TypeReuse.ExcludeTypes);
    }

    [Fact]
    public void TypeReuse_OnlyExcludeTypes()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeReuse:
              excludeTypes:
                - "SomeLib.SomeType"
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Empty(config.TypeReuse.ExcludeNamespaces);
        Assert.Equal(["SomeLib.SomeType"], config.TypeReuse.ExcludeTypes);
    }

    [Fact]
    public void TypeReuse_PreservedThroughConfigReconstruction()
    {
        var yaml = """
            spec: ./petstore.yaml
            namespace: MyApi.Generated
            clientName: PetApi
            typeReuse:
              includeNamespaces:
                - "SomeLib.*"
              excludeNamespaces:
                - "Microsoft.AspNetCore.*"
              excludeTypes:
                - "SomeLib.SomeType"
            """;

        var (loadedConfig, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);
        Assert.NotNull(loadedConfig);

        var reconstructed = new ApiStitchConfig
        {
            Spec = loadedConfig.Spec,
            Namespace = loadedConfig.Namespace,
            OutputDir = loadedConfig.OutputDir,
            OutputStyle = loadedConfig.OutputStyle,
            ClientName = loadedConfig.ClientName,
            TypeReuse = loadedConfig.TypeReuse,
        };

        Assert.Equal(["SomeLib.*"], reconstructed.TypeReuse.IncludeNamespaces);
        Assert.Equal(["Microsoft.AspNetCore.*"], reconstructed.TypeReuse.ExcludeNamespaces);
        Assert.Equal(["SomeLib.SomeType"], reconstructed.TypeReuse.ExcludeTypes);
    }

    [Fact]
    public void TypeReuse_NamespaceMap()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeReuse:
              namespaceMap:
                SampleApi.Models: Consumer.SharedModels
                OtherLib.Types: Consumer.Types
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal("Consumer.SharedModels", config.TypeReuse.NamespaceMap["SampleApi.Models"]);
        Assert.Equal("Consumer.Types", config.TypeReuse.NamespaceMap["OtherLib.Types"]);
    }

    [Fact]
    public void TypeReuse_NamespaceMap_AbsentDefaultsToEmpty()
    {
        var yaml = "spec: ./petstore.yaml";

        var (config, _, _) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(config.TypeReuse.NamespaceMap);
    }

    [Fact]
    public void TypeReuse_EmptyAndWhitespaceEntries_Filtered()
    {
        var yaml = """
            spec: ./petstore.yaml
            typeReuse:
              excludeNamespaces:
                - ""
                - "  "
                - "System.*"
              excludeTypes:
                - ""
                - "SomeLib.SomeType"
            """;

        var (config, _, diagnostics) = ConfigLoader.LoadFromYaml(yaml);

        Assert.NotNull(config);
        Assert.Empty(diagnostics);
        Assert.Equal(["System.*"], config.TypeReuse.ExcludeNamespaces);
        Assert.Equal(["SomeLib.SomeType"], config.TypeReuse.ExcludeTypes);
    }
}
