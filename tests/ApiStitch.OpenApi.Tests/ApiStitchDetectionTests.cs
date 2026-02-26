namespace ApiStitch.OpenApi.Tests;

public class ApiStitchDetectionTests
{
    [Fact]
    public void IsOpenApiGenerationOnly_ReturnsFalse_UnderNormalExecution()
    {
        ApiStitchDetection.IsOpenApiGenerationOnly.Should().BeFalse();
    }
}
