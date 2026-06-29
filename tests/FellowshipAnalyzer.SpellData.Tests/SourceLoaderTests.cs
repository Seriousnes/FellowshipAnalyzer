using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class SourceLoaderTests
{
    [Theory]
    [InlineData(1031, SpellKind.Ability, 1031)]
    [InlineData(1_001_396, SpellKind.Effect, 1396)]
    [InlineData(2_002_303, SpellKind.Talent, 2303)]
    [InlineData(3_000_155, SpellKind.Weapon, 155)]
    public void KindRange_DecodesFslId(int fslId, SpellKind kind, int nativeId)
    {
        SpellKindRange.FromFslId(fslId).ShouldBe(kind);
        SpellKindRange.NativeId(fslId).ShouldBe(nativeId);
    }

    [Fact]
    public void SpellData_LoadsBurstingIceAbility()
    {
        var s = SpellDataSource.Load(SourcePaths.SpellData);
        s.Abilities[1031].DevName.ShouldStartWith("GA_Rime_");
    }

    [Fact]
    public void GearData_LoadsVoidbringerWeapon()
    {
        var g = GearDataSource.Load(SourcePaths.GearData);
        var v = g.Weapons.Single(w => w.DisplayName == "Voidbringer's Touch");
        v.FslId.ShouldBe(155);
        v.Scalars["Cooldown"].ShouldBe(90);
        v.Scalars["MaxRange"].ShouldBe(3000);
    }

    [Fact]
    public void HeroData_LoadsRimeKitAndConstants()
    {
        var h = HeroDataSource.Load(SourcePaths.HeroData);
        var rime = h.Heroes.Single(x => x.DisplayName == "Rime");
        rime.Kit.ShouldContain(k => k.FslId == 1027);
        rime.Constants.ShouldContain(c => c.DevName == "GA_Rime_ChanneledBeamSingleDamage");
        rime.Resources.CostTypeToResource["SpiritPoints"].ShouldBe("spirit");
        rime.Resources.CostTypeToResource["ResourcesTertiary"].ShouldBe("winterOrb");
    }

    [Fact]
    public void Icons_LoadByFslGuid()
    {
        var icons = IconSource.Load(SourcePaths.Abilities);
        icons.IconFor(9).ShouldBe("T_Nhance_RPG_Icons_FrozenStep.jpg");
    }
}
