using FellowshipAnalyzer.Core.Common.Spells;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

public class SpellTypeTests
{
    [Fact]
    public void InitializerForm_CarriesScalarsAndCosts()
    {
        var s = new Spell { Id = 1027, Name = "Freezing Torrent", Cooldown = 15, Range = 30, ChannelDuration = 2.0, ChannelTickInterval = 0.4 };
        s.Cooldown.ShouldBe(15);
        s.Range.ShouldBe(30);
        s.ChannelDuration.ShouldBe(2.0);
        s.ChannelTickInterval.ShouldBe(0.4);

        var g = new Spell { Id = 1028, WinterOrbCost = 2 };
        g.WinterOrbCost.ShouldBe(2);
    }

    [Theory]
    [InlineData(1396, 1_001_396)]
    public void Effect_AppliesGuidOffset(int id, int expectedGuid) =>
        new Effect { Id = id }.Guid.ShouldBe(expectedGuid);

    [Theory]
    [InlineData(2303, 2_002_303)]
    public void Talent_AppliesGuidOffset(int id, int expectedGuid) =>
        new Talent { Id = id }.Guid.ShouldBe(expectedGuid);

    [Theory]
    [InlineData(155, 3_000_155)]
    public void Weapon_AppliesGuidOffset(int id, int expectedGuid) =>
        new Weapon { Id = id }.Guid.ShouldBe(expectedGuid);

    [Theory]
    [InlineData(1027, typeof(Spell), 1027)]
    [InlineData(1_001_396, typeof(Effect), 1_001_396)]
    [InlineData(2_002_303, typeof(Talent), 2_002_303)]
    [InlineData(3_000_155, typeof(Weapon), 3_000_155)]
    public void FromGuid_DecodesEveryRange(int guid, System.Type type, int expectedGuid)
    {
        var s = Spell.FromGuid(guid);
        s.ShouldBeOfType(type);
        s.Guid.ShouldBe(expectedGuid);
    }
}
