using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class CostMappingTests
{
    private static readonly ResourceModel Rime = new(new Dictionary<string, ResourceTypes>
    {
        ["SpiritPoints"] = ResourceTypes.Spirit,
        ["ResourcesTertiary"] = ResourceTypes.Tertiary,
        ["Resources"] = ResourceTypes.Primary,
    });

    [Fact]
    public void GlacialBlast_OrbCostMapsToTertiary() =>
        Costs.Map(new Dictionary<string, double> { ["OrbCost"] = 2.0 }, Rime)[ResourceTypes.Tertiary].ShouldBe(2);

    [Fact]
    public void Ultimate_SpiritPointCostMapsToSpirit() =>
        Costs.Map(new Dictionary<string, double> { ["SpiritPointCost"] = 100.0 }, Rime)[ResourceTypes.Spirit].ShouldBe(100);
}
