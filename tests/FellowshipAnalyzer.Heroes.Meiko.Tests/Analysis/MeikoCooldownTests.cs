using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Shouldly;

using Xunit;

using Legendaries = FellowshipAnalyzer.Core.Common.Spells.Meiko.Legendaries;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

using static FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis.MeikoAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class MeikoCooldownTests
{
    private const int HasteRating = 1_000;
    private const int LegendaryQuality = 6;
    private const int EmeraldRankTen = 1_500;

    [Fact]
    public async Task EarthfistBarrage_CastsSecondsApart_StartNoCooldown()
    {
        var parser = await Analyze(
            Cast(PullStart + 3_000, Spells.EarthfistBarrage),
            Cast(PullStart + 5_400, Spells.EarthfistBarrage),
            Cast(PullStart + 16_400, Spells.EarthfistBarrage));

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();

        parser.Events.OfType<UpdateSpellUsableEvent>()
            .ShouldNotContain(update => update.Ability.Id == Spells.EarthfistBarrage.FSLID);
        spellUsable.Drifts.ShouldBeEmpty();
        spellUsable.IsOnCooldown(Spells.EarthfistBarrage.FSLID).ShouldBeFalse();
        parser.GetModule<DebugAnnotations>().ShouldNotBeNull().GetAll()
            .SelectMany(module => module.AnnotatedEvents)
            .SelectMany(annotated => annotated.Annotations)
            .ShouldNotContain(annotation => annotation.Summary.Contains("Used with no charges available"));
        parser.Events.OfType<GlobalCooldownEvent>()
            .ShouldNotContain(gcd => gcd.Ability.Id == Spells.EarthfistBarrage.FSLID);
    }

    [Fact]
    public async Task EarthFist_AtHaste_HasAThousandMillisecondGlobalCooldown()
    {
        var combatant = Combatant();
        combatant.Haste = HasteRating;

        var parser = await AnalyzeAs(combatant, Cast(PullStart + 1_000, Spells.EarthFist));

        StatTracker.RatingToPercentage(HasteRating).ShouldBeGreaterThan(0);
        parser.Events.OfType<GlobalCooldownEvent>().ShouldHaveSingleItem().Duration.ShouldBe(1_000);
    }

    [Fact]
    public async Task EmeraldAndLegendary_StoneShieldRechargesWithCooldownRecoveryOnlyAndStaggerKeepsItsBase()
    {
        var combatant = Combatant();
        combatant.Emerald = EmeraldRankTen;
        combatant.Gear = [new Item { Id = Legendaries.EarthenSkin.Id, Quality = LegendaryQuality }];

        var parser = await AnalyzeAs(
            combatant,
            Cast(PullStart + 1_000, Spells.StoneShieldAlt),
            Cast(PullStart + 1_000, Spells.Stagger));

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();

        spellUsable.RechargeDuration(Spells.StoneShieldAlt.FSLID).ShouldBe(27_273);
        spellUsable.RechargeDuration(Spells.Stagger.FSLID).ShouldBe(12_000);
    }
}
