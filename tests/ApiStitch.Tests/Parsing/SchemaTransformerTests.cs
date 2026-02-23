using ApiStitch.Diagnostics;
using ApiStitch.Model;
using ApiStitch.Parsing;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace ApiStitch.Tests.Parsing;

public class SchemaTransformerTests
{
    private static OpenApiDocument ParseYaml(string yaml)
    {
        var reader = new OpenApiStringReader();
        var doc = reader.Read(yaml, out var diagnostic);
        return doc;
    }

    [Fact]
    public void SimpleObject_TransformsProperties()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
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
                  required: [id, name]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        var pet = Assert.Single(spec.Schemas);
        Assert.Equal("Pet", pet.Name);
        Assert.Equal(SchemaKind.Object, pet.Kind);
        Assert.Equal(2, pet.Properties.Count);

        var id = pet.Properties.First(p => p.Name == "id");
        Assert.True(id.IsRequired);
        Assert.Equal(PrimitiveType.Int64, id.Schema.PrimitiveType);

        var name = pet.Properties.First(p => p.Name == "name");
        Assert.True(name.IsRequired);
        Assert.Equal(PrimitiveType.String, name.Schema.PrimitiveType);
    }

    [Fact]
    public void RefProperty_SharedInstance()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Order:
                  type: object
                  properties:
                    pet:
                      $ref: '#/components/schemas/Pet'
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var order = spec.Schemas.First(s => s.Name == "Order");
        var pet = spec.Schemas.First(s => s.Name == "Pet");
        var orderPetProp = order.Properties.First(p => p.Name == "pet");

        Assert.Same(pet, orderPetProp.Schema);
    }

    [Fact]
    public void RequiredNullable_Matrix()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Test:
                  type: object
                  properties:
                    reqNonNull:
                      type: string
                    reqNull:
                      type: string
                      nullable: true
                    optNonNull:
                      type: string
                    optNull:
                      type: string
                      nullable: true
                  required: [reqNonNull, reqNull]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);
        var test = spec.Schemas.First(s => s.Name == "Test");

        var reqNonNull = test.Properties.First(p => p.Name == "reqNonNull");
        Assert.True(reqNonNull.IsRequired);
        Assert.False(reqNonNull.IsNullable);

        var reqNull = test.Properties.First(p => p.Name == "reqNull");
        Assert.True(reqNull.IsRequired);
        Assert.True(reqNull.IsNullable);

        var optNonNull = test.Properties.First(p => p.Name == "optNonNull");
        Assert.False(optNonNull.IsRequired);
        Assert.True(optNonNull.IsNullable);

        var optNull = test.Properties.First(p => p.Name == "optNull");
        Assert.False(optNull.IsRequired);
        Assert.True(optNull.IsNullable);
    }

    [Fact]
    public void StringEnum_TransformsCorrectly()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Status:
                  type: string
                  enum: [active, inactive, pending]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var status = Assert.Single(spec.Schemas);
        Assert.Equal(SchemaKind.Enum, status.Kind);
        Assert.Equal(3, status.EnumValues.Count);
        Assert.Equal("active", status.EnumValues[0].Name);
        Assert.Equal("Active", status.EnumValues[0].CSharpName);
    }

    [Fact]
    public void IntegerEnum_FallsToPrimitive()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Priority:
                  type: integer
                  enum: [1, 2, 3]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        var priority = Assert.Single(spec.Schemas);
        Assert.Equal(SchemaKind.Primitive, priority.Kind);
        Assert.Contains(diags, d => d.Code == "AS200");
    }

    [Fact]
    public void ArraySchema_TransformsCorrectly()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                PetList:
                  type: array
                  items:
                    $ref: '#/components/schemas/Pet'
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var petList = spec.Schemas.First(s => s.Name == "PetList");
        Assert.Equal(SchemaKind.Array, petList.Kind);
        Assert.NotNull(petList.ArrayItemSchema);
        Assert.Equal("Pet", petList.ArrayItemSchema!.Name);
    }

    [Fact]
    public void SnakeCase_ToPascalCase()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                pet_status:
                  type: string
                  enum: [active]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var status = Assert.Single(spec.Schemas);
        Assert.Equal("PetStatus", status.Name);
        Assert.Equal("pet_status", status.OriginalName);
    }

    [Fact]
    public void PropertyNames_PascalCased()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                User:
                  type: object
                  properties:
                    first_name:
                      type: string
                    last-name:
                      type: string
                    emailAddress:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var user = Assert.Single(spec.Schemas);
        Assert.Equal("FirstName", user.Properties.First(p => p.Name == "first_name").CSharpName);
        Assert.Equal("LastName", user.Properties.First(p => p.Name == "last-name").CSharpName);
        Assert.Equal("EmailAddress", user.Properties.First(p => p.Name == "emailAddress").CSharpName);
    }

    [Fact]
    public void PrimitiveTypeMapping_AllFormats()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Types:
                  type: object
                  properties:
                    str: { type: string }
                    dt: { type: string, format: date-time }
                    d: { type: string, format: date }
                    t: { type: string, format: time }
                    dur: { type: string, format: duration }
                    uuid: { type: string, format: uuid }
                    uri: { type: string, format: uri }
                    b64: { type: string, format: byte }
                    bin: { type: string, format: binary }
                    i32: { type: integer }
                    i64: { type: integer, format: int64 }
                    f: { type: number, format: float }
                    dbl: { type: number }
                    dec: { type: number, format: decimal }
                    bool: { type: boolean }
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var types = spec.Schemas.First(s => s.Name == "Types");
        Assert.Equal(PrimitiveType.String, types.Properties.First(p => p.Name == "str").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.DateTimeOffset, types.Properties.First(p => p.Name == "dt").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.DateOnly, types.Properties.First(p => p.Name == "d").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.TimeOnly, types.Properties.First(p => p.Name == "t").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.TimeSpan, types.Properties.First(p => p.Name == "dur").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Guid, types.Properties.First(p => p.Name == "uuid").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Uri, types.Properties.First(p => p.Name == "uri").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.ByteArray, types.Properties.First(p => p.Name == "b64").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.ByteArray, types.Properties.First(p => p.Name == "bin").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Int32, types.Properties.First(p => p.Name == "i32").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Int64, types.Properties.First(p => p.Name == "i64").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Float, types.Properties.First(p => p.Name == "f").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Double, types.Properties.First(p => p.Name == "dbl").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Decimal, types.Properties.First(p => p.Name == "dec").Schema.PrimitiveType);
        Assert.Equal(PrimitiveType.Bool, types.Properties.First(p => p.Name == "bool").Schema.PrimitiveType);
    }

    [Fact]
    public void UnknownFormat_WarningAndString()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Custom:
                  type: object
                  properties:
                    field:
                      type: string
                      format: custom-thing
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        var field = spec.Schemas.First().Properties.First();
        Assert.Equal(PrimitiveType.String, field.Schema.PrimitiveType);
        Assert.Contains(diags, d => d.Code == "AS204");
    }

    [Fact]
    public void AllOf_Flattening()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Base:
                  type: object
                  properties:
                    id:
                      type: integer
                Extended:
                  allOf:
                    - $ref: '#/components/schemas/Base'
                    - type: object
                      properties:
                        name:
                          type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var extended = spec.Schemas.First(s => s.Name == "Extended");
        Assert.Equal(SchemaKind.Object, extended.Kind);
        Assert.Contains(extended.Properties, p => p.Name == "id");
        Assert.Contains(extended.Properties, p => p.Name == "name");
    }

    [Fact]
    public void Deprecation_PassThrough()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Old:
                  type: object
                  deprecated: true
                  properties:
                    field:
                      type: string
                      deprecated: true
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var old = Assert.Single(spec.Schemas);
        Assert.True(old.IsDeprecated);
        Assert.True(old.Properties.First().IsDeprecated);
    }

    [Fact]
    public void Description_PassThrough()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  description: A pet in the store
                  properties:
                    name:
                      type: string
                      description: The pet's name
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var pet = Assert.Single(spec.Schemas);
        Assert.Equal("A pet in the store", pet.Description);
        Assert.Equal("The pet's name", pet.Properties.First().Description);
    }

    [Fact]
    public void EmptyObject_NoError()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Empty:
                  type: object
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        var empty = Assert.Single(spec.Schemas);
        Assert.Equal(SchemaKind.Object, empty.Kind);
        Assert.Empty(empty.Properties);
        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void PrimitiveAlias_TransformsCorrectly()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Identifier:
                  type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var id = Assert.Single(spec.Schemas);
        Assert.Equal(SchemaKind.Primitive, id.Kind);
        Assert.Equal(PrimitiveType.String, id.PrimitiveType);
    }

    [Fact]
    public void Operations_EmptyList()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        Assert.NotNull(spec.Operations);
        Assert.Empty(spec.Operations);
    }

    [Fact]
    public void CircularReference_BrokenWithWarning()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Node:
                  type: object
                  properties:
                    child:
                      $ref: '#/components/schemas/Node'
                  required: [child]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        var node = spec.Schemas.First(s => s.Name == "Node");
        var childProp = node.Properties.First(p => p.Name == "child");
        Assert.False(childProp.IsRequired);
        Assert.Contains(diags, d => d.Code == "AS003");
    }

    [Fact]
    public void AdditionalProperties_Explicit_SetCorrectly()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Dynamic:
                  type: object
                  properties:
                    name:
                      type: string
                  additionalProperties:
                    type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var dynamic = Assert.Single(spec.Schemas);
        Assert.True(dynamic.HasAdditionalProperties);
    }

    [Fact]
    public void NoAdditionalProperties_DefaultFalse()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Simple:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var simple = Assert.Single(spec.Schemas);
        Assert.False(simple.HasAdditionalProperties);
    }

    [Fact]
    public void InlineSchemaHoisting_ProducesNamedSchema()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Order:
                  type: object
                  properties:
                    shipping_address:
                      type: object
                      properties:
                        street:
                          type: string
                        city:
                          type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var hoisted = spec.Schemas.FirstOrDefault(s => s.Name == "OrderShippingAddress");
        Assert.NotNull(hoisted);
        Assert.Equal(SchemaKind.Object, hoisted!.Kind);
        Assert.Contains(hoisted.Properties, p => p.CSharpName == "Street");
        Assert.Contains(hoisted.Properties, p => p.CSharpName == "City");

        var order = spec.Schemas.First(s => s.Name == "Order");
        var addrProp = order.Properties.First(p => p.CSharpName == "ShippingAddress");
        Assert.Same(hoisted, addrProp.Schema);
    }

    [Fact]
    public void InlineSchemaHoisting_ComponentNameCollision_AS203()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Order:
                  type: object
                  properties:
                    item:
                      type: object
                      properties:
                        quantity:
                          type: integer
                OrderItem:
                  type: object
                  properties:
                    sku:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        Assert.True(diags.Any(d => d.Code == "AS202" || d.Code == "AS203"),
            "A name collision diagnostic (AS202 or AS203) should be emitted");
        Assert.Equal(3, spec.Schemas.Count);
        var names = spec.Schemas.Select(s => s.Name).OrderBy(n => n).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void SchemaNameCollision_AS203()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                pet_store:
                  type: object
                  properties:
                    name:
                      type: string
                petStore:
                  type: object
                  properties:
                    address:
                      type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        Assert.Contains(diags, d => d.Code == "AS203");
        Assert.Equal(2, spec.Schemas.Count);
        var names = spec.Schemas.Select(s => s.Name).ToList();
        Assert.Contains("PetStore", names);
        Assert.True(names.Any(n => n.StartsWith("PetStore") && n != "PetStore"),
            "Colliding schema should get an ordinal suffix");
    }

    [Fact]
    public void AllOf_PropertyConflict_AS201()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Base:
                  type: object
                  properties:
                    name:
                      type: string
                Extended:
                  allOf:
                    - $ref: '#/components/schemas/Base'
                    - type: object
                      properties:
                        name:
                          type: integer
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        Assert.Contains(diags, d => d.Code == "AS201");
        var extended = spec.Schemas.First(s => s.Name == "Extended");
        var nameProp = extended.Properties.First(p => p.Name == "name");
        Assert.Equal(PrimitiveType.Int32, nameProp.Schema.PrimitiveType);
    }

    [Fact]
    public void ThreeNodeCircularReference_BrokenWithWarning()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Alpha:
                  type: object
                  properties:
                    beta:
                      $ref: '#/components/schemas/Beta'
                  required: [beta]
                Beta:
                  type: object
                  properties:
                    gamma:
                      $ref: '#/components/schemas/Gamma'
                  required: [gamma]
                Gamma:
                  type: object
                  properties:
                    alpha:
                      $ref: '#/components/schemas/Alpha'
                  required: [alpha]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        Assert.Contains(diags, d => d.Code == "AS003");

        var allRequired = spec.Schemas
            .SelectMany(s => s.Properties.Select(p => (s.Name, p.CSharpName, p.IsRequired)))
            .ToList();
        var relaxedCount = allRequired.Count(x => !x.IsRequired);
        Assert.Equal(1, relaxedCount);
    }

    [Fact]
    public void KebabCase_SchemaName_ToPascalCase()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                pet-status:
                  type: string
                  enum: [active]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var status = Assert.Single(spec.Schemas);
        Assert.Equal("PetStatus", status.Name);
        Assert.Equal("pet-status", status.OriginalName);
    }

    [Fact]
    public void AllOf_OnlyInlineSchemas_MergesProperties()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Combined:
                  allOf:
                    - type: object
                      properties:
                        name:
                          type: string
                    - type: object
                      properties:
                        age:
                          type: integer
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);

        var combined = Assert.Single(spec.Schemas);
        Assert.Equal(SchemaKind.Object, combined.Kind);
        Assert.Contains(combined.Properties, p => p.CSharpName == "Name");
        Assert.Contains(combined.Properties, p => p.CSharpName == "Age");
        Assert.Null(combined.AllOfRefTarget);
    }

    [Fact]
    public void TwoNodeCircularReference_BrokenWithWarning()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Person:
                  type: object
                  properties:
                    spouse:
                      $ref: '#/components/schemas/Spouse'
                  required: [spouse]
                Spouse:
                  type: object
                  properties:
                    partner:
                      $ref: '#/components/schemas/Person'
                  required: [partner]
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, diags) = transformer.Transform(doc);

        Assert.Contains(diags, d => d.Code == "AS003");

        var allProps = spec.Schemas
            .SelectMany(s => s.Properties.Select(p => (s.Name, p.CSharpName, p.IsRequired)))
            .ToList();
        var relaxedCount = allProps.Count(x => !x.IsRequired);
        Assert.Equal(1, relaxedCount);
    }
}
