using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class SourceLoaderTests
{
    private static ExportSource Export() => ExportSource.Load(SourcePaths.Entities, SourcePaths.Settings);

    [Theory]
    [InlineData(155, SpellKind.Ability, 155)]
    [InlineData(1_001_396, SpellKind.Effect, 1_396)]
    [InlineData(2_000_042, SpellKind.Talent, 42)]
    [InlineData(3_000_007, SpellKind.Weapon, 7)]
    public void KindRange_DecodesFslId(int fslId, SpellKind kind, int nativeId)
    {
        var fslid = new FSLID(fslId);
        fslid.Kind.ShouldBe(kind);
        fslid.NativeId.ShouldBe(nativeId);
    }

    [Fact]
    public void ExportRoot_IsTheHighestNumberedBuildFolder() =>
        Path.GetFileName(SourcePaths.ExportRoot).ShouldStartWith("v");

    [Fact]
    public void Export_LoadsAnAbilityWithItsFirstClassScalars()
    {
        var engulfingFlames = Export().Abilities[1586];

        engulfingFlames.Name.ShouldBe("Engulfing Flames");
        engulfingFlames.Category.ShouldBe("Core");
        engulfingFlames.Cooldown.ShouldBe(20);
        engulfingFlames.ChargeCount.ShouldBe(2);
        engulfingFlames.CastTime.ShouldBe(1.5);
        engulfingFlames.Range.ShouldBe(3000);
        engulfingFlames.RangeYards.ShouldBe(30);
        engulfingFlames.Schools.ShouldBe(["Magic / Fire"]);
        engulfingFlames.Heroes.ShouldBe(["Ardeos"]);
    }

    [Fact]
    public void Export_KeysAbilitiesAndEffectsSeparately()
    {
        var export = Export();
        export.Abilities[1586].Name.ShouldBe("Engulfing Flames");
        export.Effects[1586].PartOf!.Id.ShouldBe(1129);
    }

    [Fact]
    public void Export_DefaultsChargesToOne() =>
        Export().Abilities[1027].ChargeCount.ShouldBe(1);

    [Fact]
    public void Export_ListsEveryShippedHero()
    {
        var names = Export().Heroes.Select(h => h.Name).ToList();

        names.Count.ShouldBe(12);
        names.ShouldAllBe(n => Enum.IsDefined(Enum.Parse<HeroName>(n)));
        names.ShouldContain("Rime");
    }

    [Fact]
    public void Export_ResolvesEveryAbilityCategoryItDeclares()
    {
        var export = Export();

        export.CategoryFor("Relic").ShouldBe(AbilityCategory.Relic);
        export.CategoryFor("Weapon").ShouldBe(AbilityCategory.Weapon);
        export.CategoryFor("None").ShouldBeNull();
        export.CategoryFor(null).ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => export.CategoryFor("Necklace"));
    }

    [Fact]
    public void Export_PutsRimeChannelledBeamInHerKit() =>
        Export().Abilities[1027].Heroes.ShouldContain("Rime");

    [Fact]
    public void Icons_LoadByFslGuid()
    {
        var icons = IconSource.Load(SourcePaths.Abilities);
        icons.IconFor(9).ShouldBe("T_Nhance_RPG_Icons_FrozenStep.jpg");
    }
}
