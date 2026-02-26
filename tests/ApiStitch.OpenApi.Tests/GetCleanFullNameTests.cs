namespace ApiStitch.OpenApi.Tests;

public class GetCleanFullNameTests
{
    [Fact]
    public void NonGenericType_ReturnsFullName()
    {
        ApiStitchTypeInfoSchemaTransformer.GetCleanFullName(typeof(SampleClass))
            .Should().Be("ApiStitch.OpenApi.Tests.SampleClass");
    }

    [Fact]
    public void ClosedGeneric_ReturnsCleanFormat()
    {
        ApiStitchTypeInfoSchemaTransformer.GetCleanFullName(typeof(PagedResult<SampleClass>))
            .Should().Be("ApiStitch.OpenApi.Tests.PagedResult<ApiStitch.OpenApi.Tests.SampleClass>");
    }

    [Fact]
    public void MultiTypeArgGeneric_ReturnsCleanFormat()
    {
        ApiStitchTypeInfoSchemaTransformer.GetCleanFullName(typeof(Result<SampleClass, string>))
            .Should().Be("ApiStitch.OpenApi.Tests.Result<ApiStitch.OpenApi.Tests.SampleClass, System.String>");
    }

    [Fact]
    public void NestedGeneric_ResolvesRecursively()
    {
        ApiStitchTypeInfoSchemaTransformer.GetCleanFullName(typeof(PagedResult<Result<SampleClass, string>>))
            .Should().Be("ApiStitch.OpenApi.Tests.PagedResult<ApiStitch.OpenApi.Tests.Result<ApiStitch.OpenApi.Tests.SampleClass, System.String>>");
    }

    [Fact]
    public void NullableValueType_UnwrapsBeforeFormatting()
    {
        ApiStitchTypeInfoSchemaTransformer.GetCleanFullName(typeof(SampleEnum?))
            .Should().Be("ApiStitch.OpenApi.Tests.SampleEnum");
    }

    [Fact]
    public void NullableValueType_NonGenericUnwrap()
    {
        ApiStitchTypeInfoSchemaTransformer.GetCleanFullName(typeof(int?))
            .Should().Be("System.Int32");
    }
}
