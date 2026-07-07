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
        var hero = HeroDataSource.Load(SourcePaths.HeroData).Heroes.Single(h => h.DisplayName == "Rime");
        var coldSnap = hero.Kit.Single(k => k.DevName == "GA_Rime_InstantSingleDamage");
        var merged = Linking.ConstantsFor(coldSnap, hero);
        merged.ShouldContain(c => c.Scalars.ContainsKey("OrbReward"));
        merged.ShouldContain(c => c.Scalars.ContainsKey("OrbGain"));
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
