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
        OutputStyle = OutputStyle.TypedClientFlat,
    };

    private static ApiSpecification CreateSpec(
        string clientName,
        IReadOnlyList<ApiOperation> operations,
        IReadOnlyList<ApiSchema>? schemas = null,
        bool hasProblemDetailsSupport = false)
    {
        return new ApiSpecification
        {
            Schemas = schemas ?? [],
            Operations = operations,
            ClientName = clientName,
            HasProblemDetailsSupport = hasProblemDetailsSupport,
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
    public void FileNaming_LowercaseTag_IsNormalizedToPascalCase()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "listPets",
            Path = "pets",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "pet",
            CSharpMethodName = "ListPetsAsync",
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
                Schema = petSchema,
            },
        };

        var spec = CreateSpec("PetStore", [op]);
        var emitter = new ScribanClientEmitter();
        var result = emitter.Emit(spec, DefaultConfig);

        Assert.Contains(result.Files, f => f.RelativePath == "IPetStorePetClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "PetStorePetClient.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "IPetStorepetClient.cs");
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "PetStorepetClient.cs");
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
                    Style = ParameterStyle.Form,
                    Explode = true,
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
    public void QueryParameterOrder_PreservesSpecOrderInMethodSignature()
    {
        var stringSchema = new ApiSchema
        {
            Name = "string",
            OriginalName = "string",
            Kind = SchemaKind.Primitive,
            PrimitiveType = PrimitiveType.String,
            CSharpTypeName = "string",
        };

        var op = new ApiOperation
        {
            OperationId = "loginUser",
            Path = "user/login",
            HttpMethod = ApiHttpMethod.Get,
            Tag = "User",
            CSharpMethodName = "LoginUserAsync",
            Parameters =
            [
                new ApiParameter
                {
                    Name = "username",
                    CSharpName = "username",
                    Location = ParameterLocation.Query,
                    Schema = stringSchema,
                    IsRequired = false,
                    Style = ParameterStyle.Form,
                    Explode = true,
                },
                new ApiParameter
                {
                    Name = "password",
                    CSharpName = "password",
                    Location = ParameterLocation.Query,
                    Schema = stringSchema,
                    IsRequired = false,
                    Style = ParameterStyle.Form,
                    Explode = true,
                },
            ],
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
                Schema = stringSchema,
            },
        };

        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);

        var contract = result.Files.First(f => f.RelativePath == "ITestApiUserClient.cs");
        Assert.Contains("LoginUserAsync(string? username = null, string? password = null, CancellationToken cancellationToken = default)", contract.Content);

        var impl = result.Files.First(f => f.RelativePath == "TestApiUserClient.cs");
        Assert.Contains("LoginUserAsync(string? username = null, string? password = null, CancellationToken cancellationToken = default)", impl.Content);
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
                    Style = ParameterStyle.Form,
                    Explode = true,
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
                    Style = ParameterStyle.Simple,
                    Explode = false,
                },
            ],
            SuccessResponse = new ApiResponse
            {
                StatusCode = 200,
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
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

    // ──────────────────────────────────────────────
    // Content Kind Emission
    // ──────────────────────────────────────────────

    [Fact]
    public void FormEncodedBody_EmitsFormUrlEncodedContent()
    {
        var formSchema = new ApiSchema
        {
            Name = "formBody", OriginalName = "formBody", Kind = SchemaKind.Object,
            Properties =
            [
                new ApiProperty { Name = "grant_type", CSharpName = "GrantType", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" }, IsRequired = true },
                new ApiProperty { Name = "scope", CSharpName = "Scope", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" }, IsRequired = false },
            ],
        };
        var op = new ApiOperation
        {
            OperationId = "createToken", Path = "token", HttpMethod = ApiHttpMethod.Post, Tag = "Auth", CSharpMethodName = "CreateTokenAsync",
            RequestBody = new ApiRequestBody { ContentKind = ContentKind.FormUrlEncoded, MediaType = "application/x-www-form-urlencoded", Schema = formSchema, IsRequired = true },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("FormUrlEncodedContent", impl.Content);
        Assert.Contains("grant_type", impl.Content);
    }

    [Fact]
    public void MultipartBody_EmitsMultipartFormDataContent()
    {
        var multipartSchema = new ApiSchema
        {
            Name = "multipartBody", OriginalName = "multipartBody", Kind = SchemaKind.Object,
            Properties =
            [
                new ApiProperty { Name = "file", CSharpName = "File", Schema = new ApiSchema { Name = "file", OriginalName = "file", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.Stream, CSharpTypeName = "Stream" }, IsRequired = true },
                new ApiProperty { Name = "description", CSharpName = "Description", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" }, IsRequired = false },
            ],
        };
        var op = new ApiOperation
        {
            OperationId = "uploadFile", Path = "upload", HttpMethod = ApiHttpMethod.Post, Tag = "Files", CSharpMethodName = "UploadFileAsync",
            RequestBody = new ApiRequestBody { ContentKind = ContentKind.MultipartFormData, MediaType = "multipart/form-data", Schema = multipartSchema, IsRequired = true },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("MultipartFormDataContent", impl.Content);
        Assert.Contains("StreamContent", impl.Content);
        Assert.Contains("fileFileName", impl.Content);
    }

    [Fact]
    public void MultipartBody_JsonEncodedProperty_EmitsJsonContent()
    {
        var multipartSchema = new ApiSchema
        {
            Name = "multipartBody", OriginalName = "multipartBody", Kind = SchemaKind.Object,
            Properties =
            [
                new ApiProperty { Name = "file", CSharpName = "File", Schema = new ApiSchema { Name = "file", OriginalName = "file", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.Stream, CSharpTypeName = "Stream" }, IsRequired = true },
                new ApiProperty { Name = "metadata", CSharpName = "Metadata", Schema = new ApiSchema { Name = "string", OriginalName = "string", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" }, IsRequired = false },
            ],
        };
        var encodings = new Dictionary<string, MultipartEncoding>
        {
            ["metadata"] = new MultipartEncoding { ContentType = "application/json" },
        };
        var op = new ApiOperation
        {
            OperationId = "uploadFile", Path = "upload", HttpMethod = ApiHttpMethod.Post, Tag = "Files", CSharpMethodName = "UploadFileAsync",
            RequestBody = new ApiRequestBody { ContentKind = ContentKind.MultipartFormData, MediaType = "multipart/form-data", Schema = multipartSchema, IsRequired = true, PropertyEncodings = encodings },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("JsonContent.Create(metadata", impl.Content);
    }

    [Fact]
    public void OctetStreamBody_EmitsStreamContent()
    {
        var streamSchema = new ApiSchema { Name = "body", OriginalName = "body", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.Stream, CSharpTypeName = "Stream" };
        var op = new ApiOperation
        {
            OperationId = "uploadRaw", Path = "upload", HttpMethod = ApiHttpMethod.Post, Tag = "Files", CSharpMethodName = "UploadRawAsync",
            RequestBody = new ApiRequestBody { ContentKind = ContentKind.OctetStream, MediaType = "application/octet-stream", Schema = streamSchema, IsRequired = true },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("new StreamContent(body)", impl.Content);
        Assert.Contains("application/octet-stream", impl.Content);
    }

    [Fact]
    public void PlainTextBody_EmitsStringContent()
    {
        var stringSchema = new ApiSchema { Name = "body", OriginalName = "body", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" };
        var op = new ApiOperation
        {
            OperationId = "createNote", Path = "notes", HttpMethod = ApiHttpMethod.Post, Tag = "Notes", CSharpMethodName = "CreateNoteAsync",
            RequestBody = new ApiRequestBody { ContentKind = ContentKind.PlainText, MediaType = "text/plain", Schema = stringSchema, IsRequired = true },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("new StringContent(body, Encoding.UTF8, \"text/plain\")", impl.Content);
    }

    [Fact]
    public void StreamResponse_EmitsFileResponseAndHeadersRead()
    {
        var streamSchema = new ApiSchema { Name = "response", OriginalName = "response", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.Stream, CSharpTypeName = "Stream" };
        var op = new ApiOperation
        {
            OperationId = "downloadReport", Path = "report", HttpMethod = ApiHttpMethod.Get, Tag = "Reports", CSharpMethodName = "DownloadReportAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.OctetStream, MediaType = "application/pdf", Schema = streamSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("HttpCompletionOption.ResponseHeadersRead", impl.Content);
        Assert.Contains("FileResponse.CreateAsync", impl.Content);
        Assert.Contains("Task<FileResponse>", impl.Content);
        Assert.Contains(result.Files, f => f.RelativePath == "FileResponse.cs");
    }

    [Fact]
    public void PlainTextResponse_EmitsReadAsString()
    {
        var stringSchema = new ApiSchema { Name = "response", OriginalName = "response", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" };
        var op = new ApiOperation
        {
            OperationId = "getHealth", Path = "health", HttpMethod = ApiHttpMethod.Get, Tag = "System", CSharpMethodName = "GetHealthAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.PlainText, MediaType = "text/plain", Schema = stringSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("ReadAsStringAsync", impl.Content);
        Assert.Contains("Task<string>", impl.Content);
    }

    [Fact]
    public void JsonStringResponse_EmitsTolerantStringFallbackPath()
    {
        var stringSchema = new ApiSchema { Name = "response", OriginalName = "response", Kind = SchemaKind.Primitive, PrimitiveType = PrimitiveType.String, CSharpTypeName = "string" };
        var op = new ApiOperation
        {
            OperationId = "loginUser", Path = "user/login", HttpMethod = ApiHttpMethod.Get, Tag = "User", CSharpMethodName = "LoginUserAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = stringSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("var rawResponse = await response.Content.ReadAsStringAsync", impl.Content);
        Assert.Contains("JsonSerializer.Deserialize<string>(trimmedRawResponse", impl.Content);
        Assert.Contains("return rawResponse;", impl.Content);
        Assert.DoesNotContain("ReadFromJsonAsync<string>", impl.Content);
    }

    [Fact]
    public void OptionalJsonBody_EmitsNullableBodyParameter()
    {
        var userSchema = CreateSchema("User");
        var userListSchema = new ApiSchema
        {
            Name = "UserList",
            OriginalName = "userList",
            Kind = SchemaKind.Array,
            ArrayItemSchema = userSchema,
        };

        var createUser = new ApiOperation
        {
            OperationId = "createUser", Path = "user", HttpMethod = ApiHttpMethod.Post, Tag = "User", CSharpMethodName = "CreateUserAsync",
            RequestBody = new ApiRequestBody
            {
                Schema = userSchema,
                IsRequired = false,
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
            },
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = userSchema },
        };

        var createUsers = new ApiOperation
        {
            OperationId = "createUsersWithListInput", Path = "user/createWithList", HttpMethod = ApiHttpMethod.Post, Tag = "User", CSharpMethodName = "CreateUsersWithListInputAsync",
            RequestBody = new ApiRequestBody
            {
                Schema = userListSchema,
                IsRequired = false,
                ContentKind = ContentKind.Json,
                MediaType = "application/json",
            },
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = userSchema },
        };

        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [createUser, createUsers]), DefaultConfig);

        var contract = result.Files.First(f => f.RelativePath == "ITestApiUserClient.cs");
        Assert.Contains("Task<User> CreateUserAsync(User? body = null", contract.Content);
        Assert.Contains("Task<User> CreateUsersWithListInputAsync(IReadOnlyList<User>? body = null", contract.Content);

        var impl = result.Files.First(f => f.RelativePath == "TestApiUserClient.cs");
        Assert.Contains("Task<User> CreateUserAsync(User? body = null", impl.Content);
        Assert.Contains("Task<User> CreateUsersWithListInputAsync(IReadOnlyList<User>? body = null", impl.Content);
    }

    [Fact]
    public void AcceptHeader_EmittedForResponseBody()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "getPet", Path = "pets", HttpMethod = ApiHttpMethod.Get, Tag = "Pets", CSharpMethodName = "GetPetAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = petSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("MediaTypeWithQualityHeaderValue(\"application/json\")", impl.Content);
    }

    [Fact]
    public void ContentTypeValidation_EmittedForJsonResponse()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "getPet", Path = "pets", HttpMethod = ApiHttpMethod.Get, Tag = "Pets", CSharpMethodName = "GetPetAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = petSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("Expected application/json response but received", impl.Content);
    }

    [Fact]
    public void ProblemDetails_EmittedAndReferencedInEnsureSuccess()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "getPet", Path = "pets", HttpMethod = ApiHttpMethod.Get, Tag = "Pets", CSharpMethodName = "GetPetAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = petSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op], hasProblemDetailsSupport: true), DefaultConfig);
        Assert.Contains(result.Files, f => f.RelativePath == "ProblemDetails.cs");
        var problemFile = result.Files.First(f => f.RelativePath == "ProblemDetails.cs");
        Assert.Contains("public sealed record ProblemDetails", problemFile.Content);
        Assert.Contains("[JsonPropertyName(\"type\")]", problemFile.Content);
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.Contains("Deserialize<ProblemDetails>", impl.Content);
        Assert.Contains("application/problem+json", impl.Content);
    }

    [Fact]
    public void ProblemDetails_NotEmittedWithoutSignal()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "getPet", Path = "pets", HttpMethod = ApiHttpMethod.Get, Tag = "Pets", CSharpMethodName = "GetPetAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = petSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op], hasProblemDetailsSupport: false), DefaultConfig);
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "ProblemDetails.cs");
        var impl = result.Files.First(f => f.RelativePath.Contains("Client.cs") && !f.RelativePath.StartsWith("I"));
        Assert.DoesNotContain("Deserialize<ProblemDetails>", impl.Content);
        var exceptionFile = result.Files.First(f => f.RelativePath == "ApiException.cs");
        Assert.DoesNotContain("ProblemDetails? Problem", exceptionFile.Content);
        Assert.DoesNotContain("Structured problem details from the response", exceptionFile.Content);
    }

    [Fact]
    public void NoStreamResponse_NoFileResponseEmitted()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "getPet", Path = "pets", HttpMethod = ApiHttpMethod.Get, Tag = "Pets", CSharpMethodName = "GetPetAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = petSchema },
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op]), DefaultConfig);
        Assert.DoesNotContain(result.Files, f => f.RelativePath == "FileResponse.cs");
    }

    [Fact]
    public void StructuredLayout_RoutesClientFilesToExpectedFolders()
    {
        var petSchema = CreateSchema("Pet");
        var op = new ApiOperation
        {
            OperationId = "getPet", Path = "pets", HttpMethod = ApiHttpMethod.Get, Tag = "Pets", CSharpMethodName = "GetPetAsync",
            SuccessResponse = new ApiResponse { StatusCode = 200, ContentKind = ContentKind.Json, MediaType = "application/json", Schema = petSchema },
        };

        var config = new ApiStitchConfig
        {
            Spec = DefaultConfig.Spec,
            Namespace = DefaultConfig.Namespace,
            OutputStyle = OutputStyle.TypedClientStructured,
        };
        var result = new ScribanClientEmitter().Emit(CreateSpec("TestApi", [op], hasProblemDetailsSupport: true), config);

        Assert.Contains(result.Files, f => f.RelativePath == "Contracts/ITestApiPetsClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Clients/TestApiPetsClient.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Infrastructure/ApiException.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Infrastructure/ProblemDetails.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Configuration/TestApiClientOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Configuration/TestApiJsonOptions.cs");
        Assert.Contains(result.Files, f => f.RelativePath == "Configuration/TestApiServiceCollectionExtensions.cs");
    }
}
