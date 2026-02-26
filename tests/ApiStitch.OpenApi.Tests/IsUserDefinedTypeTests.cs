namespace ApiStitch.OpenApi.Tests;

public class IsUserDefinedTypeTests
{
    [Fact]
    public void UserDefinedClass_ReturnsTrue()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(SampleClass))
            .Should().BeTrue();
    }

    [Fact]
    public void UserDefinedEnum_ReturnsTrue()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(SampleEnum))
            .Should().BeTrue();
    }

    [Fact]
    public void UserDefinedStruct_ReturnsTrue()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(SampleStruct))
            .Should().BeTrue();
    }

    [Fact]
    public void ClosedGenericType_ReturnsTrue()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(PagedResult<SampleClass>))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(long))]
    [InlineData(typeof(double))]
    [InlineData(typeof(float))]
    [InlineData(typeof(char))]
    public void PrimitiveTypes_ReturnFalse(Type type)
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(type)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(object))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(TimeOnly))]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(Uri))]
    [InlineData(typeof(Half))]
    public void WellKnownTypes_ReturnFalse(Type type)
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(type)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(int?))]
    [InlineData(typeof(DateTime?))]
    public void NullableWrappers_AreUnwrapped(Type type)
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(type)
            .Should().BeFalse();
    }

    [Fact]
    public void NullableUserDefinedEnum_ReturnsTrue()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(SampleEnum?))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(List<SampleClass>))]
    [InlineData(typeof(IList<SampleClass>))]
    [InlineData(typeof(ICollection<SampleClass>))]
    [InlineData(typeof(IEnumerable<SampleClass>))]
    [InlineData(typeof(IReadOnlyList<SampleClass>))]
    [InlineData(typeof(IReadOnlyCollection<SampleClass>))]
    [InlineData(typeof(HashSet<SampleClass>))]
    [InlineData(typeof(Dictionary<string, SampleClass>))]
    [InlineData(typeof(IDictionary<string, SampleClass>))]
    [InlineData(typeof(IReadOnlyDictionary<string, SampleClass>))]
    public void CollectionGenericTypes_ReturnFalse(Type type)
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(type)
            .Should().BeFalse();
    }

    [Fact]
    public void NonCollectionClosedGeneric_ReturnsTrue()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(Result<SampleClass, string>))
            .Should().BeTrue();
    }

    [Fact]
    public void Array_ReturnsFalse()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(SampleClass[]))
            .Should().BeFalse();
    }

    [Fact]
    public void ByteArray_ReturnsFalse()
    {
        ApiStitchTypeInfoSchemaTransformer.IsUserDefinedType(typeof(byte[]))
            .Should().BeFalse();
    }
}

public class SampleClass
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public enum SampleEnum { A, B }

public struct SampleStruct
{
    public decimal Amount { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public class Result<TValue, TError>
{
    public TValue? Value { get; set; }
    public TError? Error { get; set; }
}
