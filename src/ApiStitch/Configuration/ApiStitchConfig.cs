namespace ApiStitch.Configuration;

public class ApiStitchConfig
{
    public required string Spec { get; init; }
    public string Namespace { get; init; } = "ApiStitch.Generated";
    public string OutputDir { get; init; } = "./Generated";
}
