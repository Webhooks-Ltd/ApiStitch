using ApiStitch.Configuration;
using ApiStitch.Emission;
using ApiStitch.Model;

namespace ApiStitch.Tests.Emission;

public class ScribanClientEmitterTests
{
    private static readonly ApiStitchConfig DefaultConfig = new()
    {
        Spec = "test.yaml",
        Namespace = "TestApi.Generated",
    };

    private static ApiSpecification CreateSpec(
        string clientName,
        IReadOnlyList<ApiOperation> operations,
        IReadOnlyList<ApiSchema>? schemas = null)
    {
        return new ApiSpecification
        {
            Schemas = schemas ?? [],
            Operations = operations,
            ClientName = clientName,
        };
    }

    private static ApiSchema CreateSchema(string name, SchemaKind kind = SchemaKind.Object)
    {
        return new ApiSchema
        {
            Name = name,
            OriginalName = name.ToLowerInvariant(),
            Kind = kind,
            CSharpTypeName = name,
        };
    }

    [Fact]
    public void FileNaming_SingleTag_ProducesExpectedFileNames()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "ListPetsAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = petSchema,
            },
        };

        var spec = CreateSpec("PetStoreApi", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Contains(result.Files, f => f.RelativePath == "IPetStoreApiPetsClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStoreApiPetsClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "ApiException.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStoreApiClientOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStoreApiJsonOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStoreApiServiceCollectionExtensions.cs");
    }

    [Fact]
    public void FileNaming_DefaultTag_OmitsTagFromFileName()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "PetStoreApi",
            CSharpMethodName = "ListPetsAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = petSchema,
            },
        };

        var spec = CreateSpec("PetStoreApi", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Contains(result.Files, f => f.RelativePath == "IPetStoreApiClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStoreApiClient.cs");
    }

    [Fact]
    public void SingleTagApi_RendersInterfaceAndImplementation()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "ListPetsAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = petSchema,
            },
        };

        var spec = CreateSpec("TestApi", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        var interfaceFile = result.Files.First(f => f.RelativePath == "ITestApiPetsClient.cs");
        Assert.Contains("public partial interface ITestApiPetsClient", interfaceFile.Content);

        var implFile = result.Files.First(f => f.RelativePath == "TestApiPetsClient.cs");
        Assert.Contains("internal sealed partial class TestApiPetsClient : ITestApiPetsClient", implFile.Content);
        Assert.Contains("IHttpClientFactory", implFile.Content);

        Assert.Contains(result.Files, f => f.RelativePath == "ApiException.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiClientOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiJsonOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiServiceCollectionExtensions.cs");
    }

    [Fact]
    public void MultiTagApi_ProducesClientPerTagAndSharedFiles()
    {
        var petSchema = CreateSchema("Pet");
        var orderSchema = CreateSchema("Order");

        var petOp = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "ListPetsAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = petSchema,
            },
        };

        var storeOp = new ApiOperation
        {
            OperationId = "getOrder",
            Path = "store/order",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Store",
            CSharpMethodName = "GetOrderAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = orderSchema,
            },
        };

        var spec = CreateSpec("TestApi", [petOp, storeOp]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Contains(result.Files, f => f.RelativePath == "ITestApiPetsClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiPetsClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "ITestApiStoreClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiStoreClient.cs");

        Assert.Contains(result.Files, f => f.RelativePath == "ApiException.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiClientOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiJsonOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "TestApiServiceCollectionExtensions.cs");

        Assert.Single(result.Files, f => f.RelativePath == "ApiException.cs");
    }

    [Fact]
    public void EmptyOperations_ReturnsNoFiles()
    {
        var spec = CreateSpec("TestApi", []);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Empty(result.Files);
    }

    [Fact]
    public void EnumQueryParam_ProducesExtensionsFile()
    {
        var statusSchema = new ApiSchema
        {
            Name = "PetStatus",
            OriginalName = "petStatus",
            Kind = SchemaKind.Enum,
            EnumValues =
            [
                new ApiEnumMember { Name = "available", CSharpName = "Available" },
                new ApiEnumMember { Name = "pending", CSharpName = "Pending" },
                new ApiEnumMember { Name = "sold", CSharpName = "Sold" },
            ],
        };

        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "ListPetsAsync",
            Parameters =
            [
                new ApiParameter
                {
                    Name = "status",
                    CSharpName = "status",
                    Location = ParameterLocation.Query,
                    Schema = statusSchema,
                    IsRequired = false,
                },
            ],
        };

        var spec = CreateSpec("TestApi", [op], [statusSchema]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Contains(result.Files, f => f.RelativePath == "PetStatusExtensions.cs");
    }

    [Fact]
    public void NoEnumQueryParam_NoExtensionsFile()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "ListPetsAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = petSchema,
            },
        };

        var spec = CreateSpec("TestApi", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.DoesNotContain(result.Files, f =>
            f.RelativePath.EndsWith("Extensions.cs")
            && !f.RelativePath.Contains("ServiceCollectionExtensions"));
    }

    [Fact]
    public void ExternalEnumQueryParam_NoExtensionsFile_UsesToString()
    {
        var statusSchema = new ApiSchema
        {
            Name = "PetStatus",
            OriginalName = "petStatus",
            Kind = SchemaKind.Enum,
            ExternalClrTypeName = "SampleApi.Models.PetStatus",
            CSharpTypeName = "SampleApi.Models.PetStatus",
            EnumValues =
            [
                new ApiEnumMember { Name = "available", CSharpName = "Available" },
            ],
        };

        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "ListPetsAsync",
            Parameters =
            [
                new ApiParameter
                {
                    Name = "status",
                    CSharpName = "status",
                    Location = ParameterLocation.Query,
                    Schema = statusSchema,
                    IsRequired = false,
                },
            ],
        };

        var spec = CreateSpec("TestApi", [op], [statusSchema]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.DoesNotContain(result.Files, f => f.RelativePath == "PetStatusExtensions.cs");
        var implFile = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains(".ToString()", implFile.Content);
        Assert.DoesNotContain(".ToQueryString()", implFile.Content);
    }

    [Fact]
    public void ExternalType_InReturnType_UsesFQN()
    {
        var petSchema = new ApiSchema
        {
            Name = "Pet",
            OriginalName = "pet",
            Kind = SchemaKind.Object,
            ExternalClrTypeName = "SampleApi.Models.Pet",
            CSharpTypeName = "SampleApi.Models.Pet",
        };

        var op = new ApiOperation
        {
            OperationId = "getPet",
            Path = "pets/{id}",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "Pets",
            CSharpMethodName = "GetPetAsync",
            Parameters =
            [
                new ApiParameter
                {
                    Name = "id",
                    CSharpName = "id",
                    Location = ParameterLocation.Path,
                    Schema = new ApiSchema { Name = "int", OriginalName = "int", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.Int32, CSharpTypeName = "int" },
                    IsRequired = true,
                },
            ],
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Schema = petSchema,
            },
        };

        var spec = CreateSpec("TestApi", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        var interfaceFile = result.Files.First(f => f.RelativePath.StartsWith("I") && f.RelativePath.Contains("Client"));
        Assert.Contains("Task<SampleApi.Models.Pet>", interfaceFile.Content);
    }

    [Fact]
    public void ExternalType_InRequestBody_UsesFQN()
    {
        var requestSchema = new ApiSchema
        {
            Name = "CreatePetRequest",
            OriginalName = "createPetRequest",
            Kind = SchemaKind.Object,
            ExternalClrTypeName = "SampleApi.Models.CreatePetRequest",
            CSharpTypeName = "SampleApi.Models.CreatePetRequest",
        };

        var op = new ApiOperation
        {
            OperationId = "createPet",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Post,
            Tag = "Pets",
            CSharpMethodName = "CreatePetAsync",
            RequestBody = new ApiRequestBody
            {
                ContentType = "application/json",
                Schema = requestSchema,
                IsRequired = true,
            },
        };

        var spec = CreateSpec("TestApi", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        var interfaceFile = result.Files.First(f => f.RelativePath.StartsWith("I") && f.RelativePath.Contains("Client"));
        Assert.Contains("SampleApi.Models.CreatePetRequest body", interfaceFile.Content);
    }
}
