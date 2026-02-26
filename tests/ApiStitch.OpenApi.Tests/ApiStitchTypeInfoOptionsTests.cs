namespace ApiStitch.OpenApi.Tests;

public class ApiStitchTypeInfoOptionsTests
{
    [Fact]
    public void AlwaysEmit_DefaultsToFalse()
    {
        var options = new ApiStitchTypeInfoOptions();
        options.AlwaysEmit.Should().BeFalse();
    }
}
