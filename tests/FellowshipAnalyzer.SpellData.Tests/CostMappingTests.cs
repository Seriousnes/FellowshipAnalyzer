using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class CostMappingTests
{
    private static readonly ResourceModel Rime = new(new Dictionary<string, string>
    {
        ["SpiritPoints"] = "spirit", ["ResourcesTertiary"] = "winterOrb", ["Resources"] = "anima",
    });

    [Fact]
    public void GlacialBlast_OrbCostMapsToWinterOrb() =>
        Costs.Map(new Dictionary<string, double> { ["OrbCost"] = 2.0 }, Rime)["winterOrb"].ShouldBe(2);

    [Fact]
    public void Ultimate_SpiritPointCostMapsToSpirit() =>
        Costs.Map(new Dictionary<string, double> { ["SpiritPointCost"] = 100.0 }, Rime)["spirit"].ShouldBe(100);
}
