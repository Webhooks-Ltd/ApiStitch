using ApiStitch.Configuration;
using ApiStitch.Emission;
using ApiStitch.Model;

namespace ApiStitch.Tests.Emission;

public class ScribanModelEmitterTests
{
    private static ApiStitchConfig DefaultConfig => new()
    {
        Spec = "test.yaml",
        Namespace = "TestApi.Models",
    };

    [Fact]
    public void ExternalObjectSchema_SkippedInFiles_IncludedInJsonContext()
    {
        var external = new ApiSchema
        {
            Name = "Pet", OriginalName = "Pet", Kind = SchemaKind.Object,
            ExternalClrTypeName = "SampleApi.Models.Pet",
            CSharpTypeName = "SampleApi.Models.Pet",
        };
        var spec = new ApiSpecification { Schemas = [external], Operations = [] };

        var emitter = new ScribanModelEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Pet.cs");
        var contextFile = Assert.Single(result.Files, f => f.RelativePath.EndsWith("JsonContext.cs"));
        Assert.Contains("SampleApi.Models.Pet", contextFile.Content);
    }

    [Fact]
    public void ExternalEnumSchema_SkippedInFiles_IncludedInJsonContext()
    {
        var external = new ApiSchema
        {
            Name = "PetStatus", OriginalName = "PetStatus", Kind = SchemaKind.Enum,
            ExternalClrTypeName = "SampleApi.Models.PetStatus",
            CSharpTypeName = "SampleApi.Models.PetStatus",
            EnumValues = [new ApiEnumMember { Name = "available", CSharpName = "Available" }],
        };
        var spec = new ApiSpecification { Schemas = [external], Operations = [] };

        var emitter = new ScribanModelEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "PetStatus.cs");
        var contextFile = Assert.Single(result.Files, f => f.RelativePath.EndsWith("JsonContext.cs"));
        Assert.Contains("SampleApi.Models.PetStatus", contextFile.Content);
    }

    [Fact]
    public void MixedExternalAndLocal_OnlyLocalEmitted()
    {
        var external = new ApiSchema
        {
            Name = "Pet", OriginalName = "Pet", Kind = SchemaKind.Object,
            ExternalClrTypeName = "SampleApi.Models.Pet",
            CSharpTypeName = "SampleApi.Models.Pet",
        };
        var local = new ApiSchema
        {
            Name = "Category", OriginalName = "Category", Kind = SchemaKind.Object,
            CSharpTypeName = "Category",
            Properties = [new ApiProperty { Name = "name", CSharpName = "Name", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String }, IsRequired = true }],
        };
        var spec = new ApiSpecification { Schemas = [external, local], Operations = [] };

        var emitter = new ScribanModelEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Pet.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Category.cs");
        var contextFile = Assert.Single(result.Files, f => f.RelativePath.EndsWith("JsonContext.cs"));
        Assert.Contains("SampleApi.Models.Pet", contextFile.Content);
        Assert.Contains("Category", contextFile.Content);
    }

    [Fact]
    public void ExternalBase_UsesFQNInDerivedRecord()
    {
        var baseSchema = new ApiSchema
        {
            Name = "Animal", OriginalName = "Animal", Kind = SchemaKind.Object,
            ExternalClrTypeName = "SharedModels.Animal",
            CSharpTypeName = "SharedModels.Animal",
        };
        var derived = new ApiSchema
        {
            Name = "Dog", OriginalName = "Dog", Kind = SchemaKind.Object,
            CSharpTypeName = "Dog",
            BaseSchema = baseSchema,
            Properties = [new ApiProperty { Name = "breed", CSharpName = "Breed", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String }, IsRequired = true }],
        };
        var spec = new ApiSpecification { Schemas = [baseSchema, derived], Operations = [] };

        var emitter = new ScribanModelEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Animal.cs");
        var dogFile = Assert.Single(result.Files, f => f.RelativePath == "Dog.cs");
        Assert.Contains("SharedModels.Animal", dogFile.Content);
        Assert.Contains("sealed", dogFile.Content);
    }

    [Fact]
    public void ExternalDerived_NonExternalBase_BaseEmittedNormally()
    {
        var baseSchema = new ApiSchema
        {
            Name = "Animal", OriginalName = "Animal", Kind = SchemaKind.Object,
            CSharpTypeName = "Animal",
            Properties = [new ApiProperty { Name = "name", CSharpName = "Name", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String }, IsRequired = true }],
        };
        var derived = new ApiSchema
        {
            Name = "Dog", OriginalName = "Dog", Kind = SchemaKind.Object,
            ExternalClrTypeName = "SharedModels.Dog",
            CSharpTypeName = "SharedModels.Dog",
            BaseSchema = baseSchema,
        };
        var spec = new ApiSpecification { Schemas = [baseSchema, derived], Operations = [] };

        var emitter = new ScribanModelEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Contains(result.Files, f => f.RelativePath == "Animal.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "Dog.cs");
        var contextFile = Assert.Single(result.Files, f => f.RelativePath.EndsWith("JsonContext.cs"));
        Assert.Contains("Animal", contextFile.Content);
        Assert.Contains("SharedModels.Dog", contextFile.Content);
    }
}
