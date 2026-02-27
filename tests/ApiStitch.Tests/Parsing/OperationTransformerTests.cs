using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Model;
using ApiStitch.Parsing;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using ParameterLocation = ApiStitch.Model.ParameterLocation;
using ParameterStyle = ApiStitch.Model.ParameterStyle;

namespace ApiStitch.Tests.Parsing;

public class OperationTransformerTests
{
    private static readonly OpenApiReaderSettings YamlSettings = CreateYamlSettings();
    private static OpenApiReaderSettings CreateYamlSettings() { var s = new OpenApiReaderSettings(); s.AddYamlReader(); return s; }

    private static OpenApiDocument ParseYaml(string yaml)
    {
        return OpenApiDocument.Parse(yaml, settings: YamlSettings).Document!;
    }

    private static (IReadOnlyList<ApiOperation> Operations, string ClientName, IReadOnlyList<Diagnostic> Diagnostics) TransformOperations(
        string yaml, ApiStitchConfig? config = null)
    {
        var doc = ParseYaml(yaml);
        var transformer = new SchemaTransformer();
        var (spec, schemaMap, _) = transformer.Transform(doc);
        config ??= new ApiStitchConfig { Spec = "test.yaml" };
        return OperationTransformer.Transform(doc, schemaMap, config);
    }

    // ──────────────────────────────────────────────
    // 6.1 Basic operation parsing
    // ──────────────────────────────────────────────

    [Fact]
    public void Get_WithOperationIdAndTag_ParsesCorrectly()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Pet'
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    id:
                      type: integer
                      format: int64
                    name:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.Equal("pets", op.Path);
        Assert.Equal(ApiHttpMethod.Get, op.HttpMethod);
        Assert.Equal("listPets", op.OperationId);
        Assert.Equal("Pets", op.Tag);
        Assert.Equal("ListPetsAsync", op.CSharpMethodName);
    }

    [Fact]
    public void Post_WithJsonRefBody_HasRequestBody()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                post:
                  operationId: createPet
                  tags: [Pets]
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/Pet'
                  responses:
                    '201':
                      description: Created
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    id:
                      type: integer
                      format: int64
                    name:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal("Pet", op.RequestBody!.Schema.Name);
        Assert.True(op.RequestBody.IsRequired);
        Assert.Equal(ContentKind.Json, op.RequestBody.ContentKind);
        Assert.Equal("application/json", op.RequestBody.MediaType);
    }

    [Fact]
    public void Delete_204_HasNoBody()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                delete:
                  operationId: deletePet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '204':
                      description: No Content
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(204, op.SuccessResponse!.StatusCode);
        Assert.False(op.SuccessResponse.HasBody);
    }

    // ──────────────────────────────────────────────
    // 6.2 Parameter classification
    // ──────────────────────────────────────────────

    [Fact]
    public void Parameters_ClassifiedByLocation()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  operationId: getPet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                    - name: status
                      in: query
                      schema:
                        type: string
                    - name: X-Request-Id
                      in: header
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Equal(3, op.Parameters.Count);

        var pathParam = op.Parameters.First(p => p.Name == "petId");
        Assert.Equal(ParameterLocation.Path, pathParam.Location);

        var queryParam = op.Parameters.First(p => p.Name == "status");
        Assert.Equal(ParameterLocation.Query, queryParam.Location);

        var headerParam = op.Parameters.First(p => p.Name == "X-Request-Id");
        Assert.Equal(ParameterLocation.Header, headerParam.Location);
    }

    [Fact]
    public void CookieParameter_SkippedWithAS402()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: session
                      in: cookie
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Empty(op.Parameters);
        Assert.Contains(diags, d => d.Code == "AS402");
    }

    [Fact]
    public void PathParameter_AlwaysRequired()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  operationId: getPet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: false
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var pathParam = Assert.Single(op.Parameters);
        Assert.True(pathParam.IsRequired);
    }

    // ──────────────────────────────────────────────
    // 6.3 Schema resolution
    // ──────────────────────────────────────────────

    [Fact]
    public void Parameter_WithRefSchema_ResolvedFromSchemaMap()
    {
        var yaml = """
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: status
                      in: query
                      schema:
                        $ref: '#/components/schemas/Status'
                  responses:
                    '200':
                      description: OK
            components:
              schemas:
                Status:
                  type: string
                  enum: [active, inactive]
            """;

        var doc = ParseYaml(yaml);
        var transformer = new SchemaTransformer();
        var (spec, schemaMap, _) = transformer.Transform(doc);
        var config = new ApiStitchConfig { Spec = "test.yaml" };
        var (ops, _, _) = OperationTransformer.Transform(doc, schemaMap, config);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        var statusSchema = spec.Schemas.First(s => s.Name == "Status");
        Assert.Same(statusSchema, param.Schema);
    }

    [Fact]
    public void Parameter_InlinePrimitive_CorrectPrimitiveType()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: limit
                      in: query
                      schema:
                        type: integer
                        format: int32
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal(SchemaKind.Primitive, param.Schema.Kind);
        Assert.Equal(PrimitiveType.Int32, param.Schema.PrimitiveType);
    }

    [Fact]
    public void Parameter_InlineArray_ArrayKind()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: tags
                      in: query
                      explode: true
                      schema:
                        type: array
                        items:
                          type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal(SchemaKind.Array, param.Schema.Kind);
        Assert.NotNull(param.Schema.ArrayItemSchema);
        Assert.Equal(PrimitiveType.String, param.Schema.ArrayItemSchema!.PrimitiveType);
    }

    [Fact]
    public void Parameter_InlineComplexObject_SkippedWithAS401()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: filter
                      in: query
                      schema:
                        type: object
                        properties:
                          name:
                            type: string
                          age:
                            type: integer
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Empty(op.Parameters);
        Assert.Contains(diags, d => d.Code == "AS401");
    }

    // ──────────────────────────────────────────────
    // Parameter style/explode
    // ──────────────────────────────────────────────

    [Fact]
    public void Parameter_QueryDefault_FormExplodeTrue()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: status
                      in: query
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal(ParameterStyle.Form, param.Style);
        Assert.True(param.Explode);
    }

    [Fact]
    public void Parameter_PathDefault_SimpleFalse()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  operationId: getPet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal(ParameterStyle.Simple, param.Style);
        Assert.False(param.Explode);
    }

    [Fact]
    public void Parameter_ExplicitFormExplodeFalse_Accepted()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: colors
                      in: query
                      style: form
                      explode: false
                      schema:
                        type: array
                        items:
                          type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal(ParameterStyle.Form, param.Style);
        Assert.False(param.Explode);
    }

    [Fact]
    public void Parameter_DeepObjectExplodeTrue_AcceptsInlineObject()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: filter
                      in: query
                      style: deepObject
                      explode: true
                      schema:
                        type: object
                        properties:
                          status:
                            type: string
                          type:
                            type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal(ParameterStyle.DeepObject, param.Style);
        Assert.True(param.Explode);
        Assert.Equal(SchemaKind.Object, param.Schema.Kind);
        Assert.Equal(2, param.Schema.Properties.Count);
        Assert.DoesNotContain(diags, d => d.Code == "AS401");
    }

    [Fact]
    public void Parameter_DeepObjectExplodeFalse_SkippedWithAS407()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: filter
                      in: query
                      style: deepObject
                      explode: false
                      schema:
                        type: object
                        properties:
                          status:
                            type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Empty(op.Parameters);
        Assert.Contains(diags, d => d.Code == "AS407");
    }

    [Fact]
    public void Parameter_PipeDelimited_SkippedWithAS407()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  parameters:
                    - name: ids
                      in: query
                      style: pipeDelimited
                      schema:
                        type: array
                        items:
                          type: integer
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Empty(op.Parameters);
        Assert.Contains(diags, d => d.Code == "AS407");
    }

    // ──────────────────────────────────────────────
    // 6.4 Request body
    // ──────────────────────────────────────────────

    [Fact]
    public void RequestBody_JsonRef_MatchesSchemaMap()
    {
        var yaml = """
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                post:
                  operationId: createPet
                  tags: [Pets]
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/Pet'
                  responses:
                    '201':
                      description: Created
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """;

        var doc = ParseYaml(yaml);
        var transformer = new SchemaTransformer();
        var (spec, schemaMap, _) = transformer.Transform(doc);
        var config = new ApiStitchConfig { Spec = "test.yaml" };
        var (ops, _, _) = OperationTransformer.Transform(doc, schemaMap, config);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        var petSchema = spec.Schemas.First(s => s.Name == "Pet");
        Assert.Same(petSchema, op.RequestBody!.Schema);
    }

    [Fact]
    public void RequestBody_InlineArrayOfRef_CorrectArraySchema()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/batch:
                post:
                  operationId: createPets
                  tags: [Pets]
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          type: array
                          items:
                            $ref: '#/components/schemas/Pet'
                  responses:
                    '201':
                      description: Created
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(SchemaKind.Array, op.RequestBody!.Schema.Kind);
        Assert.NotNull(op.RequestBody.Schema.ArrayItemSchema);
        Assert.Equal("Pet", op.RequestBody.Schema.ArrayItemSchema!.Name);
    }

    [Fact]
    public void RequestBody_UnsupportedContentType_RequiredBody_AS404_OperationSkipped()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /data:
                post:
                  operationId: postXml
                  tags: [Data]
                  requestBody:
                    required: true
                    content:
                      application/xml:
                        schema:
                          type: object
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Empty(ops);
        Assert.Contains(diags, d => d.Code == "AS404");
    }

    [Fact]
    public void RequestBody_UnsupportedContentType_OptionalBody_NotSkipped()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /data:
                post:
                  operationId: postXml
                  tags: [Data]
                  requestBody:
                    required: false
                    content:
                      application/xml:
                        schema:
                          type: object
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Single(ops);
        Assert.Contains(diags, d => d.Code == "AS404");
        Assert.Null(ops[0].RequestBody);
    }

    [Fact]
    public void RequestBody_MultipartFormData_ParsesCorrectly()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /upload:
                post:
                  operationId: uploadFile
                  tags: [Files]
                  requestBody:
                    required: true
                    content:
                      multipart/form-data:
                        schema:
                          type: object
                          properties:
                            file:
                              type: string
                              format: binary
                            description:
                              type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(ContentKind.MultipartFormData, op.RequestBody!.ContentKind);
        Assert.Equal("multipart/form-data", op.RequestBody.MediaType);
        Assert.Equal(2, op.RequestBody.Schema.Properties.Count);
    }

    [Fact]
    public void RequestBody_InlineComplexSchema_AS401_OperationSkipped()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                post:
                  operationId: createPet
                  tags: [Pets]
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          type: object
                          properties:
                            name:
                              type: string
                            age:
                              type: integer
                  responses:
                    '201':
                      description: Created
            components:
              schemas: {}
            """);

        Assert.Empty(ops);
        Assert.Contains(diags, d => d.Code == "AS401");
    }

    // ──────────────────────────────────────────────
    // 6.5 Response parsing
    // ──────────────────────────────────────────────

    [Fact]
    public void Response_200WithRefBody_HasBody()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  operationId: getPet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Pet'
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(200, op.SuccessResponse!.StatusCode);
        Assert.True(op.SuccessResponse.HasBody);
        Assert.Equal("Pet", op.SuccessResponse.Schema!.Name);
    }

    [Fact]
    public void Response_204NoContent_HasBodyFalse()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                delete:
                  operationId: deletePet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '204':
                      description: No Content
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(204, op.SuccessResponse!.StatusCode);
        Assert.False(op.SuccessResponse.HasBody);
    }

    [Fact]
    public void Response_ArrayOfRef_ArraySchema()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Pet'
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(SchemaKind.Array, op.SuccessResponse!.Schema!.Kind);
        Assert.Equal("Pet", op.SuccessResponse.Schema.ArrayItemSchema!.Name);
    }

    [Fact]
    public void Response_InlineArrayOfPrimitive_ArraySchema()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/tags:
                get:
                  operationId: listTags
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: string
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(SchemaKind.Array, op.SuccessResponse!.Schema!.Kind);
        Assert.Equal(SchemaKind.Primitive, op.SuccessResponse.Schema.ArrayItemSchema!.Kind);
        Assert.Equal(PrimitiveType.String, op.SuccessResponse.Schema.ArrayItemSchema.PrimitiveType);
    }

    [Fact]
    public void Response_Multiple2xx_LowestWithBodyUsed()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                post:
                  operationId: createPet
                  tags: [Pets]
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/Pet'
                  responses:
                    '201':
                      description: Created
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Pet'
                    '202':
                      description: Accepted
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Pet'
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(201, op.SuccessResponse!.StatusCode);
        Assert.True(op.SuccessResponse.HasBody);
    }

    [Fact]
    public void Response_No2xx_SuccessResponseIsNull()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /ping:
                get:
                  operationId: ping
                  tags: [Health]
                  responses:
                    '301':
                      description: Redirect
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Null(op.SuccessResponse);
    }

    // ──────────────────────────────────────────────
    // 6.6 Method naming
    // ──────────────────────────────────────────────

    [Fact]
    public void MethodName_CamelCase_ToPascalAsync()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Equal("ListPetsAsync", ops[0].CSharpMethodName);
    }

    [Fact]
    public void MethodName_SnakeCase_ToPascalAsync()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  operationId: get_pet_by_id
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Equal("GetPetByIdAsync", ops[0].CSharpMethodName);
    }

    [Fact]
    public void MethodName_KebabCase_ToPascalAsync()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  operationId: get-pet-by-id
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Equal("GetPetByIdAsync", ops[0].CSharpMethodName);
    }

    [Fact]
    public void MethodName_DerivedFromPath_WithAS400()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                get:
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Equal("GetPetsByPetIdAsync", op.CSharpMethodName);
        Assert.Contains(diags, d => d.Code == "AS400");
    }

    [Fact]
    public void MethodName_ExistingAsyncSuffix_NoDoubleAsync()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPetsAsync
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Equal("ListPetsAsync", ops[0].CSharpMethodName);
    }

    [Fact]
    public void MethodName_Collision_DedupWithSuffix_AS403()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
              /pets/all:
                get:
                  operationId: list_pets
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Equal(2, ops.Count);
        var names = ops.Select(o => o.CSharpMethodName).ToList();
        Assert.Contains("ListPetsAsync", names);
        Assert.Contains("ListPetsAsync2", names);
        Assert.Contains(diags, d => d.Code == "AS403");
    }

    // ──────────────────────────────────────────────
    // 6.7 Tag handling
    // ──────────────────────────────────────────────

    [Fact]
    public void NoTags_TagEqualsDerivedClientName()
    {
        var (ops, clientName, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Pet Store, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.Equal(clientName, op.Tag);
    }

    [Fact]
    public void MultiTag_DuplicatedPerTag()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets, Animals]
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Equal(2, ops.Count);
        Assert.Contains(ops, o => o.Tag == "Pets");
        Assert.Contains(ops, o => o.Tag == "Animals");
        Assert.True(ops.All(o => o.OperationId == "listPets"));
    }

    [Fact]
    public void DeprecatedOperation_IsDeprecatedTrue()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets:
                get:
                  operationId: listPets
                  tags: [Pets]
                  deprecated: true
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.True(op.IsDeprecated);
    }

    // ──────────────────────────────────────────────
    // 6.8 Path-level parameter merging
    // ──────────────────────────────────────────────

    [Fact]
    public void PathLevelParam_InheritedByOperation()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                parameters:
                  - name: petId
                    in: path
                    required: true
                    schema:
                      type: string
                get:
                  operationId: getPet
                  tags: [Pets]
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal("petId", param.Name);
        Assert.Equal(ParameterLocation.Path, param.Location);
    }

    [Fact]
    public void OperationLevelParam_OverridesPathLevel()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /pets/{petId}:
                parameters:
                  - name: petId
                    in: path
                    required: true
                    schema:
                      type: string
                    description: Path-level petId
                get:
                  operationId: getPet
                  tags: [Pets]
                  parameters:
                    - name: petId
                      in: path
                      required: true
                      schema:
                        type: integer
                        format: int64
                      description: Operation-level petId
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        var param = Assert.Single(op.Parameters);
        Assert.Equal("petId", param.Name);
        Assert.Equal(PrimitiveType.Int64, param.Schema.PrimitiveType);
    }

    // ──────────────────────────────────────────────
    // 6.9 Client name derivation
    // ──────────────────────────────────────────────

    [Fact]
    public void ClientName_FromConfig_ReturnsExactly()
    {
        var config = new ApiStitchConfig { Spec = "test.yaml", ClientName = "MyCustomClient" };
        var (_, clientName, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Pet Store, version: 1.0.0 }
            paths: {}
            components:
              schemas: {}
            """, config);

        Assert.Equal("MyCustomClient", clientName);
    }

    [Fact]
    public void ClientName_FromTitle_PetStore_PetStoreApi()
    {
        var (_, clientName, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Pet Store, version: 1.0.0 }
            paths: {}
            components:
              schemas: {}
            """);

        Assert.Equal("PetStoreApi", clientName);
    }

    [Fact]
    public void ClientName_SpecialCharactersInTitle_Cleaned()
    {
        var (_, clientName, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: "My API! v2", version: 1.0.0 }
            paths: {}
            components:
              schemas: {}
            """);

        Assert.True(clientName.All(c => char.IsLetterOrDigit(c)));
        Assert.EndsWith("Api", clientName);
    }

    [Fact]
    public void ClientName_EmptyTitle_ApiClient()
    {
        var (_, clientName, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: "", version: 1.0.0 }
            paths: {}
            components:
              schemas: {}
            """);

        Assert.Equal("ApiClient", clientName);
    }

    [Fact]
    public void ClientName_NullTitle_ApiClient()
    {
        var (_, clientName, _) = TransformOperations("""
            openapi: 3.0.3
            info: { version: 1.0.0 }
            paths: {}
            components:
              schemas: {}
            """);

        Assert.Equal("ApiClient", clientName);
    }

    // ──────────────────────────────────────────────
    // Content Type Parsing
    // ──────────────────────────────────────────────

    [Fact]
    public void RequestBody_FormUrlEncoded_ParsesCorrectly()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /token:
                post:
                  operationId: createToken
                  tags: [Auth]
                  requestBody:
                    required: true
                    content:
                      application/x-www-form-urlencoded:
                        schema:
                          type: object
                          required: [grant_type]
                          properties:
                            grant_type:
                              type: string
                            scope:
                              type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(ContentKind.FormUrlEncoded, op.RequestBody!.ContentKind);
        Assert.Equal("application/x-www-form-urlencoded", op.RequestBody.MediaType);
        Assert.Equal(2, op.RequestBody.Schema.Properties.Count);
    }

    [Fact]
    public void RequestBody_OctetStream_ParsesAsStream()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /upload:
                post:
                  operationId: uploadRaw
                  tags: [Files]
                  requestBody:
                    required: true
                    content:
                      application/octet-stream:
                        schema:
                          type: string
                          format: binary
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(ContentKind.OctetStream, op.RequestBody!.ContentKind);
        Assert.Equal(PrimitiveType.Stream, op.RequestBody.Schema.PrimitiveType);
    }

    [Fact]
    public void RequestBody_PlainText_ParsesAsString()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /notes:
                post:
                  operationId: createNote
                  tags: [Notes]
                  requestBody:
                    required: true
                    content:
                      text/plain:
                        schema:
                          type: string
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(ContentKind.PlainText, op.RequestBody!.ContentKind);
        Assert.Equal(PrimitiveType.String, op.RequestBody.Schema.PrimitiveType);
    }

    [Fact]
    public void RequestBody_ContentNegotiation_PrefersJson()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /token:
                post:
                  operationId: createToken
                  tags: [Auth]
                  requestBody:
                    required: true
                    content:
                      application/x-www-form-urlencoded:
                        schema:
                          type: object
                          properties:
                            grant_type:
                              type: string
                      application/json:
                        schema:
                          $ref: '#/components/schemas/TokenRequest'
                  responses:
                    '200':
                      description: OK
            components:
              schemas:
                TokenRequest:
                  type: object
                  properties:
                    grant_type:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(ContentKind.Json, op.RequestBody!.ContentKind);
        Assert.Contains(diags, d => d.Code == "AS409");
    }

    [Fact]
    public void Response_OctetStream_ParsesAsStream()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /report:
                get:
                  operationId: downloadReport
                  tags: [Reports]
                  responses:
                    '200':
                      description: OK
                      content:
                        application/pdf:
                          schema:
                            type: string
                            format: binary
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(ContentKind.OctetStream, op.SuccessResponse!.ContentKind);
        Assert.Equal("application/pdf", op.SuccessResponse.MediaType);
        Assert.Equal(PrimitiveType.Stream, op.SuccessResponse.Schema!.PrimitiveType);
    }

    [Fact]
    public void Response_PlainText_ParsesAsString()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /health:
                get:
                  operationId: getHealth
                  tags: [System]
                  responses:
                    '200':
                      description: OK
                      content:
                        text/plain:
                          schema:
                            type: string
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(ContentKind.PlainText, op.SuccessResponse!.ContentKind);
        Assert.Equal(PrimitiveType.String, op.SuccessResponse.Schema!.PrimitiveType);
    }

    [Fact]
    public void Response_PlainText_NoSchema_SynthesizesString()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /health:
                get:
                  operationId: getHealth
                  tags: [System]
                  responses:
                    '200':
                      description: OK
                      content:
                        text/plain: {}
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(ContentKind.PlainText, op.SuccessResponse!.ContentKind);
        Assert.Equal(PrimitiveType.String, op.SuccessResponse.Schema!.PrimitiveType);
    }

    [Fact]
    public void Response_ContentNegotiation_PrefersJson()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /data:
                get:
                  operationId: getData
                  tags: [Data]
                  responses:
                    '200':
                      description: OK
                      content:
                        application/xml:
                          schema:
                            type: object
                        application/json:
                          schema:
                            $ref: '#/components/schemas/DataItem'
            components:
              schemas:
                DataItem:
                  type: object
                  properties:
                    value:
                      type: string
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.SuccessResponse);
        Assert.Equal(ContentKind.Json, op.SuccessResponse!.ContentKind);
    }

    [Fact]
    public void RequestBody_FormEncoded_ComplexRef_RejectedWithAS401()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /form:
                post:
                  operationId: postForm
                  tags: [Forms]
                  requestBody:
                    required: true
                    content:
                      application/x-www-form-urlencoded:
                        schema:
                          type: object
                          properties:
                            nested:
                              $ref: '#/components/schemas/Nested'
                  responses:
                    '200':
                      description: OK
            components:
              schemas:
                Nested:
                  type: object
                  properties:
                    value:
                      type: string
            """);

        Assert.Empty(ops);
        Assert.Contains(diags, d => d.Code == "AS401");
    }

    [Fact]
    public void RequestBody_Multipart_BinaryProperty_MapsToStream()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /upload:
                post:
                  operationId: uploadFile
                  tags: [Files]
                  requestBody:
                    required: true
                    content:
                      multipart/form-data:
                        schema:
                          type: object
                          properties:
                            file:
                              type: string
                              format: binary
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.Equal(ContentKind.MultipartFormData, op.RequestBody!.ContentKind);
        var fileProp = op.RequestBody.Schema.Properties.Single(p => p.Name == "file");
        Assert.Equal(PrimitiveType.Stream, fileProp.Schema.PrimitiveType);
    }

    [Fact]
    public void RequestBody_Multipart_Encoding_ParsedCorrectly()
    {
        var (ops, _, _) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /upload:
                post:
                  operationId: uploadFile
                  tags: [Files]
                  requestBody:
                    required: true
                    content:
                      multipart/form-data:
                        schema:
                          type: object
                          properties:
                            file:
                              type: string
                              format: binary
                            metadata:
                              type: object
                        encoding:
                          metadata:
                            contentType: application/json
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        var op = Assert.Single(ops);
        Assert.NotNull(op.RequestBody);
        Assert.NotNull(op.RequestBody!.PropertyEncodings);
        Assert.True(op.RequestBody.PropertyEncodings!.ContainsKey("metadata"));
        Assert.Equal("application/json", op.RequestBody.PropertyEncodings["metadata"].ContentType);
    }

    [Fact]
    public void RequestBody_Multipart_Encoding_UnknownProperty_EmitsAS408()
    {
        var (ops, _, diags) = TransformOperations("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths:
              /upload:
                post:
                  operationId: uploadFile
                  tags: [Files]
                  requestBody:
                    required: true
                    content:
                      multipart/form-data:
                        schema:
                          type: object
                          properties:
                            file:
                              type: string
                              format: binary
                        encoding:
                          nonExistent:
                            contentType: application/json
                  responses:
                    '200':
                      description: OK
            components:
              schemas: {}
            """);

        Assert.Single(ops);
        Assert.Contains(diags, d => d.Code == "AS408");
    }
}
