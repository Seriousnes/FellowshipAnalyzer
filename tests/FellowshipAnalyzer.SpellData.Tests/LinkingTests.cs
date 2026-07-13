using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class LinkingTests
{
    [Fact]
    public void ConstantsFor_MergesEverySharedDevName()
    {
        var hero = HeroDataSource.Load(SourcePaths.HeroData).Heroes.Single(h => h.DisplayName == "Ardeos");
        var engulfingFlames = hero.Kit.Single(k => k.DevName == "GA_Firemage_CastedSingleHeavyDot");
        var merged = Linking.ConstantsFor(engulfingFlames, hero);

        merged.Select(c => c.Key).ShouldBe(["Engulfing Flames", "CastedSingleHeavyDot"], ignoreOrder: true);
        merged.ShouldContain(c => c.Key == "Engulfing Flames" && c.Scalars.ContainsKey("Duration"));
        merged.ShouldContain(c => c.Key == "CastedSingleHeavyDot" && c.Scalars.ContainsKey("Cooldown"));
    }

    [Fact]
    public void LinkEffects_LinksBurstingIceToItsDamageEffect()
    {
        var spells = SpellDataSource.Load(SourcePaths.SpellData);
        var hero = HeroDataSource.Load(SourcePaths.HeroData).Heroes.Single(h => h.DisplayName == "Rime");
        var links = Linking.LinkEffects(hero, spells);
        links.ShouldContain(l => l.EffectFslId == 1_001_396);
    }
}
