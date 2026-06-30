using System.Text.Json;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class ResourceTypesConverterTests
{
    private static readonly JsonSerializerOptions Options =
        new() { Converters = { new ResourceTypesJsonConverter() } };

    [Fact]
    public void Resolve_ByCanonicalMemberName_IsCaseInsensitive()
    {
        ResourceTypesAliases.TryResolve("tertiary", out var a).ShouldBeTrue();
        a.ShouldBe(ResourceTypes.Tertiary);
        ResourceTypesAliases.TryResolve("Tertiary", out var b).ShouldBeTrue();
        b.ShouldBe(ResourceTypes.Tertiary);
    }

    [Fact]
    public void Resolve_ByFlavorAlias_MapsToSlot()
    {
        ResourceTypesAliases.TryResolve("Winter Orbs", out var a).ShouldBeTrue();
        a.ShouldBe(ResourceTypes.Tertiary);
        ResourceTypesAliases.TryResolve("anima", out var b).ShouldBeTrue();
        b.ShouldBe(ResourceTypes.Primary);
    }

    [Fact]
    public void Resolve_Unknown_ReturnsFalse() =>
        ResourceTypesAliases.TryResolve("plasma", out _).ShouldBeFalse();

    [Fact]
    public void ToToken_EmitsCamelCaseMemberName()
    {
        ResourceTypesAliases.ToToken(ResourceTypes.Tertiary).ShouldBe("tertiary");
        ResourceTypesAliases.ToToken(ResourceTypes.Primary).ShouldBe("primary");
        ResourceTypesAliases.ToToken(ResourceTypes.Spirit).ShouldBe("spirit");
    }

    [Fact]
    public void DictionaryKey_RoundTrips_ThroughPropertyNameMethods()
    {
        var costs = new Dictionary<ResourceTypes, int> { [ResourceTypes.Tertiary] = 2, [ResourceTypes.Spirit] = 100 };
        var json = JsonSerializer.Serialize(costs, Options);
        json.ShouldContain("\"tertiary\":2");
        json.ShouldContain("\"spirit\":100");

        var back = JsonSerializer.Deserialize<Dictionary<ResourceTypes, int>>(json, Options)!;
        back[ResourceTypes.Tertiary].ShouldBe(2);
        back[ResourceTypes.Spirit].ShouldBe(100);
    }

    [Fact]
    public void Value_RoundTrips_ThroughValueMethods()
    {
        var json = JsonSerializer.Serialize(ResourceTypes.Tertiary, Options);
        json.ShouldBe("\"tertiary\"");
        JsonSerializer.Deserialize<ResourceTypes>(json, Options).ShouldBe(ResourceTypes.Tertiary);
    }

    [Fact]
    public void BuildAliasMap_DuplicateToken_Throws()
    {
        var dup = new (ResourceTypes, IReadOnlyList<string>)[]
        {
            (ResourceTypes.Primary, new[] { "Shared" }),
            (ResourceTypes.Tertiary, new[] { "shared" }),
        };
        Should.Throw<InvalidOperationException>(() => ResourceTypesAliases.BuildAliasMap(dup));
    }

    [Fact]
    public void RealEnumMap_BuildsWithoutCollision() =>
        Should.NotThrow(() => ResourceTypesAliases.TryResolve("spirit", out _));
}
