using ApiStitch.Diagnostics;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ApiStitch.Configuration;

public static class ConfigLoader
{
    public static (ApiStitchConfig? Config, IReadOnlyList<Diagnostic> Diagnostics) Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS300", $"Configuration file not found: {filePath}")]);
        }

        string yaml;
        try
        {
            yaml = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS300", $"Unable to read configuration file: {ex.Message}")]);
        }

        return LoadFromYaml(yaml);
    }

    internal static (ApiStitchConfig? Config, IReadOnlyList<Diagnostic> Diagnostics) LoadFromYaml(string yaml)
    {
        ConfigDto dto;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            dto = deserializer.Deserialize<ConfigDto>(yaml) ?? new ConfigDto();
        }
        catch (Exception ex)
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS301", $"Invalid YAML: {ex.Message}")]);
        }

        if (string.IsNullOrWhiteSpace(dto.Spec))
        {
            return (null, [new Diagnostic(DiagnosticSeverity.Error, "AS302", "Configuration property 'spec' is required and must not be empty")]);
        }

        var config = new ApiStitchConfig
        {
            Spec = dto.Spec!,
            Namespace = string.IsNullOrWhiteSpace(dto.Namespace) ? "ApiStitch.Generated" : dto.Namespace!,
            OutputDir = string.IsNullOrWhiteSpace(dto.OutputDir) ? "./Generated" : dto.OutputDir!
        };

        return (config, []);
    }

    private class ConfigDto
    {
        public string? Spec { get; set; }
        public string? Namespace { get; set; }
        public string? OutputDir { get; set; }
    }
}
