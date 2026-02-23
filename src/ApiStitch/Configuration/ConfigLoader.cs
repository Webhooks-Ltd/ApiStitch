using ApiStitch.Diagnostics;
using ApiStitch.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ApiStitch.Configuration;

/// <summary>
/// Loads and validates ApiStitch configuration from YAML files.
/// </summary>
public static class ConfigLoader
{
    /// <summary>
    /// Loads configuration from a YAML file at the given path.
    /// </summary>
    public static (ApiStitchConfig? Config, FileWriteOptions Delivery, IReadOnlyList<Diagnostic> Diagnostics) Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (null, new FileWriteOptions(), [new Diagnostic(DiagnosticSeverity.Error, "AS300", $"Configuration file not found: {filePath}")]);
        }

        string yaml;
        try
        {
            yaml = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            return (null, new FileWriteOptions(), [new Diagnostic(DiagnosticSeverity.Error, "AS300", $"Unable to read configuration file: {ex.Message}")]);
        }

        return LoadFromYaml(yaml);
    }

    /// <summary>
    /// Parses configuration from a YAML string.
    /// </summary>
    internal static (ApiStitchConfig? Config, FileWriteOptions Delivery, IReadOnlyList<Diagnostic> Diagnostics) LoadFromYaml(string yaml)
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
            return (null, new FileWriteOptions(), [new Diagnostic(DiagnosticSeverity.Error, "AS301", $"Invalid YAML: {ex.Message}")]);
        }

        if (string.IsNullOrWhiteSpace(dto.Spec))
        {
            return (null, new FileWriteOptions(), [new Diagnostic(DiagnosticSeverity.Error, "AS302", "Configuration property 'spec' is required and must not be empty")]);
        }

        OutputStyle outputStyle = OutputStyle.TypedClient;
        var diagnostics = new List<Diagnostic>();

        if (!string.IsNullOrWhiteSpace(dto.OutputStyle))
        {
            if (!Enum.TryParse<OutputStyle>(dto.OutputStyle, ignoreCase: true, out outputStyle))
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "AS303",
                    $"Unknown output style '{dto.OutputStyle}'. Supported values: {string.Join(", ", Enum.GetNames<OutputStyle>())}"));
                return (null, new FileWriteOptions(), diagnostics);
            }
        }

        var config = new ApiStitchConfig
        {
            Spec = dto.Spec!,
            Namespace = string.IsNullOrWhiteSpace(dto.Namespace) ? "ApiStitch.Generated" : dto.Namespace!,
            OutputDir = string.IsNullOrWhiteSpace(dto.OutputDir) ? "./Generated" : dto.OutputDir!,
            OutputStyle = outputStyle,
            ClientName = string.IsNullOrWhiteSpace(dto.ClientName) ? null : dto.ClientName!.Trim(),
        };

        var delivery = new FileWriteOptions
        {
            CleanOutput = dto.Delivery?.CleanOutput ?? false,
        };

        return (config, delivery, diagnostics);
    }

    private class ConfigDto
    {
        public string? Spec { get; set; }
        public string? Namespace { get; set; }
        public string? OutputDir { get; set; }
        public string? OutputStyle { get; set; }
        public string? ClientName { get; set; }
        public DeliveryDto? Delivery { get; set; }
    }

    private class DeliveryDto
    {
        public bool? CleanOutput { get; set; }
    }
}
