using ApiStitch.Model;
using ApiStitch.TypeMapping;

namespace ApiStitch.Tests.TypeMapping;

public class CSharpTypeMapperTests
{
    [Theory]
    [InlineData(PrimitiveType.String, "string")]
    [InlineData(PrimitiveType.Int32, "int")]
    [InlineData(PrimitiveType.Int64, "long")]
    [InlineData(PrimitiveType.Float, "float")]
    [InlineData(PrimitiveType.Double, "double")]
    [InlineData(PrimitiveType.Decimal, "decimal")]
    [InlineData(PrimitiveType.Bool, "bool")]
    [InlineData(PrimitiveType.DateTimeOffset, "DateTimeOffset")]
    [InlineData(PrimitiveType.DateOnly, "DateOnly")]
    [InlineData(PrimitiveType.TimeOnly, "TimeOnly")]
    [InlineData(PrimitiveType.TimeSpan, "TimeSpan")]
    [InlineData(PrimitiveType.Guid, "Guid")]
    [InlineData(PrimitiveType.Uri, "Uri")]
    [InlineData(PrimitiveType.ByteArray, "byte[]")]
    public void PrimitiveMapping(PrimitiveType primitiveType, string expected)
    {
        Assert.Equal(expected, CSharpTypeMapper.MapPrimitive(primitiveType));
    }

    [Fact]
    public void ObjectSchema_UsesName()
    {
        var schema = new ApiSchema { Name = "Pet", OriginalName = "Pet", Kind = SchemaKind.Object };
        Assert.Equal("Pet", CSharpTypeMapper.MapSchema(schema));
    }

    [Fact]
    public void EnumSchema_UsesName()
    {
        var schema = new ApiSchema { Name = "PetStatus", OriginalName = "PetStatus", Kind = SchemaKind.Enum };
        Assert.Equal("PetStatus", CSharpTypeMapper.MapSchema(schema));
    }

    [Fact]
    public void ArrayOfRef_IReadOnlyList()
    {
        var itemSchema = new ApiSchema { Name = "Pet", OriginalName = "Pet", Kind = SchemaKind.Object };
        var arraySchema = new ApiSchema
        {
            Name = "PetList",
            OriginalName = "PetList",
            Kind = SchemaKind.Array,
            ArrayItemSchema = itemSchema
        };
        Assert.Equal("IReadOnlyList<Pet>", CSharpTypeMapper.MapSchema(arraySchema));
    }

    [Fact]
    public void MapAll_EnrichesAllSchemas()
    {
        var schemas = new List<ApiSchema>
        {
            new() { Name = "Pet", OriginalName = "Pet", Kind = SchemaKind.Object },
            new() { Name = "PetStatus", OriginalName = "PetStatus", Kind = SchemaKind.Enum },
            new() { Name = "Id", OriginalName = "Id", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.Int64 },
        };
        var spec = new ApiSpecification { Schemas = schemas };

        CSharpTypeMapper.MapAll(spec);

        Assert.Equal("Pet", schemas[0].CSharpTypeName);
        Assert.Equal("PetStatus", schemas[1].CSharpTypeName);
        Assert.Equal("long", schemas[2].CSharpTypeName);
    }
}
