using ApiStitch.Model;
using ApiStitch.Parsing;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace ApiStitch.Tests.Parsing;

public class InheritanceDetectorTests
{
    private static readonly OpenApiReaderSettings YamlSettings = CreateYamlSettings();
    private static OpenApiReaderSettings CreateYamlSettings() { var s = new OpenApiReaderSettings(); s.AddYamlReader(); return s; }

    private static OpenApiDocument ParseYaml(string yaml)
    {
        return OpenApiDocument.Parse(yaml, settings: YamlSettings).Document!;
    }

    [Fact]
    public void TwoSchemasInheritFromBase_SetsBaseSchema()
    {
        var doc = ParseYaml("""
            openapi: 3.0.3
            info: { title: Test, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Animal:
                  type: object
                  properties:
                    name:
                      type: string
                Dog:
                  allOf:
                    - $ref: '#/components/schemas/Animal'
                    - type: object
                      properties:
                        breed:
                          type: string
                Cat:
                  allOf:
                    - $ref: '#/components/schemas/Animal'
                    - type: object
                      properties:
                        indoor:
                          type: boolean
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);
        InheritanceDetector.Detect(spec);

        var animal = spec.Schemas.First(s => s.Name == "Animal");
        var dog = spec.Schemas.First(s => s.Name == "Dog");
        var cat = spec.Schemas.First(s => s.Name == "Cat");

        Assert.Same(animal, dog.BaseSchema);
        Assert.Same(animal, cat.BaseSchema);
        Assert.DoesNotContain(dog.Properties, p => p.Name == "name");
        Assert.DoesNotContain(cat.Properties, p => p.Name == "name");
        Assert.Contains(dog.Properties, p => p.Name == "breed");
        Assert.Contains(cat.Properties, p => p.Name == "indoor");
    }

    [Fact]
    public void SingleUseAllOf_NoInheritance()
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
                        extra:
                          type: string
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);
        InheritanceDetector.Detect(spec);

        var extended = spec.Schemas.First(s => s.Name == "Extended");
        Assert.Null(extended.BaseSchema);
        Assert.Contains(extended.Properties, p => p.Name == "id");
    }

    [Fact]
    public void AllOfWithEmptyInline_NoInheritance()
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
                Derived1:
                  allOf:
                    - $ref: '#/components/schemas/Base'
                    - type: object
                Derived2:
                  allOf:
                    - $ref: '#/components/schemas/Base'
                    - type: object
            """);

        var transformer = new SchemaTransformer();
        var (spec, _, _) = transformer.Transform(doc);
        InheritanceDetector.Detect(spec);

        var derived1 = spec.Schemas.First(s => s.Name == "Derived1");
        var derived2 = spec.Schemas.First(s => s.Name == "Derived2");

        Assert.Null(derived1.BaseSchema);
        Assert.Null(derived2.BaseSchema);
    }
}
