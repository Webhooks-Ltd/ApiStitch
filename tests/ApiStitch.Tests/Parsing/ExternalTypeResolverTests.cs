using ApiStitch.Configuration;
using ApiStitch.Diagnostics;
using ApiStitch.Model;
using ApiStitch.Parsing;

namespace ApiStitch.Tests.Parsing;

public class ExternalTypeResolverTests
{
    private static ApiSpecification MakeSpec(params ApiSchema[] schemas) =>
        new() { Schemas = schemas.ToList(), Operations = [] };

    private static ApiSchema MakeSchema(string name, string? vendorHint = null) =>
        new()
        {
            Name = name,
            OriginalName = name,
            Kind = SchemaKind.Object,
            VendorTypeHint = vendorHint,
        };

    private static ApiStitchConfig MakeConfig(
        List<string>? includeNamespaces = null,
        List<string>? includeTypes = null,
        List<string>? excludeNamespaces = null,
        List<string>? excludeTypes = null,
        Dictionary<string, string>? namespaceMap = null) =>
        new()
        {
            Spec = "test.yaml",
            TypeReuse = new TypeReuseConfig
            {
                IncludeNamespaces = includeNamespaces ?? [],
                IncludeTypes = includeTypes ?? [],
                ExcludeNamespaces = excludeNamespaces ?? [],
                ExcludeTypes = excludeTypes ?? [],
                NamespaceMap = namespaceMap ?? [],
            },
        };

    [Fact]
    public void NoIncludeRules_NothingReused()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig());

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
    }

    [Fact]
    public void IncludeNamespace_HintHonoured()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(includeNamespaces: ["SampleApi.Models.*"]));

        Assert.Equal("SampleApi.Models.Pet", schema.ExternalClrTypeName);
        Assert.True(schema.IsExternal);
    }

    [Fact]
    public void IncludeType_ExactMatch()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(includeTypes: ["SampleApi.Models.Pet"]));

        Assert.Equal("SampleApi.Models.Pet", schema.ExternalClrTypeName);
        Assert.True(schema.IsExternal);
    }

    [Fact]
    public void IncludeNamespace_NonMatchingHintIgnored()
    {
        var schema = MakeSchema("Thing", "OtherNamespace.Thing");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(includeNamespaces: ["SampleApi.Models.*"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
    }

    [Fact]
    public void NoVendorHint_NoOp()
    {
        var schema = MakeSchema("Pet");
        var spec = MakeSpec(schema);

        var diags = ExternalTypeResolver.Resolve(spec, MakeConfig(includeNamespaces: ["*"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
        Assert.Empty(diags);
    }

    [Fact]
    public void ExcludeOverridesInclude_ByNamespaceGlob()
    {
        var schema = MakeSchema("ProblemDetails", "Microsoft.AspNetCore.Mvc.ProblemDetails");
        var spec = MakeSpec(schema);

        var diags = ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["Microsoft.*"],
            excludeNamespaces: ["Microsoft.AspNetCore.*"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
        Assert.Empty(diags);
    }

    [Fact]
    public void ExcludeOverridesInclude_ByExactType()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        var diags = ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            excludeTypes: ["SampleApi.Models.Pet"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
        Assert.Empty(diags);
    }

    [Fact]
    public void NestedType_PlusNormalised()
    {
        var schema = MakeSchema("Inner", "SampleApi.Models.Outer+Inner");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(includeNamespaces: ["SampleApi.*"]));

        Assert.Equal("SampleApi.Models.Outer.Inner", schema.ExternalClrTypeName);
    }

    [Fact]
    public void GenericType_PlusInArgsNormalised()
    {
        var schema = MakeSchema("Container", "SampleApi.Models.Container<SampleApi.Models.Outer+Inner>");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(includeNamespaces: ["SampleApi.*"]));

        Assert.Equal("SampleApi.Models.Container<SampleApi.Models.Outer.Inner>", schema.ExternalClrTypeName);
    }

    [Fact]
    public void GlobAnchoring_SystemDoesNotMatchSystemMonitor()
    {
        var schema = MakeSchema("Foo", "SystemMonitor.Types.Foo");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SystemMonitor.*"],
            excludeNamespaces: ["System.*"]));

        Assert.Equal("SystemMonitor.Types.Foo", schema.ExternalClrTypeName);
        Assert.True(schema.IsExternal);
    }

    [Fact]
    public void GlobCaseSensitive()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            excludeNamespaces: ["sampleapi.*"]));

        Assert.Equal("SampleApi.Models.Pet", schema.ExternalClrTypeName);
        Assert.True(schema.IsExternal);
    }

    [Fact]
    public void GenericTypeWithAngleBrackets_ExcludedByGlob()
    {
        var schema = MakeSchema("PagedResult", "SampleApi.Models.PagedResult<OtherNamespace.Pet>");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            excludeNamespaces: ["SampleApi.*"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
    }

    [Fact]
    public void MultipleExcludePatterns_AnyMatchExcludes()
    {
        var schema = MakeSchema("List", "System.Collections.Generic.List");
        var spec = MakeSpec(schema);

        var diags = ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["System.*", "Microsoft.*"],
            excludeNamespaces: ["Microsoft.*", "System.*"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
        Assert.Empty(diags);
    }

    [Fact]
    public void ExclusionMatchesRawHint_BeforeNormalization()
    {
        var schema = MakeSchema("Inner", "SampleApi.Models.Outer+Inner");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            excludeNamespaces: ["SampleApi.*"]));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
    }

    [Fact]
    public void MixedSchemas_IncludeExcludeInteraction()
    {
        var external = MakeSchema("Pet", "SampleApi.Models.Pet");
        var local = MakeSchema("Category");
        var excluded = MakeSchema("Problem", "Microsoft.AspNetCore.Mvc.ProblemDetails");
        var spec = MakeSpec(external, local, excluded);

        var diags = ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*", "Microsoft.*"],
            excludeNamespaces: ["Microsoft.*"]));

        Assert.True(external.IsExternal);
        Assert.False(local.IsExternal);
        Assert.False(excluded.IsExternal);
        Assert.Single(diags);
    }

    [Fact]
    public void NamespaceMap_RemapsPrefix()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            namespaceMap: new() { ["SampleApi.Models"] = "Consumer.SharedModels" }));

        Assert.Equal("Consumer.SharedModels.Pet", schema.ExternalClrTypeName);
    }

    [Fact]
    public void NamespaceMap_NoMatchLeavesUnchanged()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            namespaceMap: new() { ["OtherNamespace"] = "Remapped" }));

        Assert.Equal("SampleApi.Models.Pet", schema.ExternalClrTypeName);
    }

    [Fact]
    public void NamespaceMap_FirstMatchWins()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            namespaceMap: new() { ["SampleApi"] = "First", ["SampleApi.Models"] = "Second" }));

        Assert.Equal("First.Models.Pet", schema.ExternalClrTypeName);
    }

    [Fact]
    public void NamespaceMap_ExactTypeNameMatch()
    {
        var schema = MakeSchema("Thing", "SampleApi.Models.Thing");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            namespaceMap: new() { ["SampleApi.Models.Thing"] = "Consumer.Thing" }));

        Assert.Equal("Consumer.Thing", schema.ExternalClrTypeName);
    }

    [Fact]
    public void NamespaceMap_AppliedAfterNestedTypeNormalization()
    {
        var schema = MakeSchema("Inner", "SampleApi.Models.Outer+Inner");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            namespaceMap: new() { ["SampleApi.Models"] = "Consumer.Models" }));

        Assert.Equal("Consumer.Models.Outer.Inner", schema.ExternalClrTypeName);
    }

    [Fact]
    public void NamespaceMap_ExclusionTakesPrecedence()
    {
        var schema = MakeSchema("Pet", "SampleApi.Models.Pet");
        var spec = MakeSpec(schema);

        ExternalTypeResolver.Resolve(spec, MakeConfig(
            includeNamespaces: ["SampleApi.*"],
            excludeTypes: ["SampleApi.Models.Pet"],
            namespaceMap: new() { ["SampleApi.Models"] = "Consumer.Models" }));

        Assert.Null(schema.ExternalClrTypeName);
        Assert.False(schema.IsExternal);
    }
}
