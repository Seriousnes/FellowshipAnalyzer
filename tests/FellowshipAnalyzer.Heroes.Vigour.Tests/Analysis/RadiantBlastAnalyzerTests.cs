using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Vigour.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class RadiantBlastAnalyzerTests
{
    [Fact]
    public async Task AvatarOfLightTimeIsNotCountedAsReady()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.AvatarOfLight, PlayerId),
            ApplyBuff(PullStart, Spells.AvatarOfLightBuff, PlayerId),
            Cast(PullStart + 1_000, Spells.RadiantBlast),
            Cast(PullStart + 2_000, Spells.RadiantBlast),
            RemoveBuff(PullStart + 9_000, Spells.AvatarOfLightBuff, PlayerId));

        var analyzer = parser.RadiantBlastAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(2);
        analyzer.AvatarCasts.ShouldBe(2);
        analyzer.ReadyMs.ShouldBeLessThanOrEqualTo(PullDuration - 9_000);
        analyzer.MeasuredMs.ShouldBe(PullDuration - 9_000);
    }

    [Fact]
    public async Task AvatarOfLightActiveAtThePullStartIsExcluded()
    {
        var parser = await Analyze(
            Cast(PullStart - 2_000, Spells.AvatarOfLight, PlayerId),
            ApplyBuff(PullStart - 2_000, Spells.AvatarOfLightBuff, PlayerId),
            Cast(PullStart + 1_000, Spells.RadiantBlast),
            RemoveBuff(PullStart + 7_000, Spells.AvatarOfLightBuff, PlayerId));

        var analyzer = parser.RadiantBlastAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AvatarCasts.ShouldBe(1);
        analyzer.MeasuredMs.ShouldBe(PullDuration - 7_000);
    }

    [Fact]
    public async Task AvatarOfLightApplicationEndsTheCooldownOfARadiantBlastOnCooldown()
    {
        var parser = await Analyze(
            Cast(2_000, Spells.RadiantBlast),
            ApplyBuff(5_000, Spells.AvatarOfLightBuff, PlayerId));

        var end = parser.Events.OfType<UpdateSpellUsableEvent>()
            .Single(e => e.Ability.Id == Spells.RadiantBlast.FSLID && e.UpdateType == UpdateSpellUsableType.EndCooldown);

        end.Timestamp.ShouldBe(5_000);
        end.ChargesAvailable.ShouldBe(1);
    }

    [Fact]
    public void AvatarOfLightBuff_IsTheEffectTheLogWritesForTheBuff()
    {
        Spells.AvatarOfLightBuff.FSLID.Value.ShouldBe(1_001_333);
        Spells.AvatarOfLightResourceCost.FSLID.Value.ShouldBe(1_001_331);
    }

    [Fact]
    public async Task AvatarOfLightBuffAsTheLogWritesIt_EndsTheCooldownAndSuppressesNewOnes()
    {
        var parser = await Analyze(
            Cast(2_000, Spells.RadiantBlast),
            LoggedAvatarBuff<ApplyBuffEvent>(5_000, effectId: 1_001_333, duration: 9_000),
            Cast(6_000, Spells.RadiantBlast));

        var updates = parser.Events.OfType<UpdateSpellUsableEvent>()
            .Where(e => e.Ability.Id == Spells.RadiantBlast.FSLID)
            .ToList();
        updates.Where(e => e.UpdateType == UpdateSpellUsableType.EndCooldown).Select(e => e.Timestamp).ShouldBe([5_000]);
        updates.Where(e => e.UpdateType == UpdateSpellUsableType.BeginCooldown).Select(e => e.Timestamp).ShouldBe([2_000]);
    }

    [Fact]
    public async Task AvatarOfLightResourceCostEffect_IsNotTheBuff()
    {
        var parser = await Analyze(
            Cast(2_000, Spells.RadiantBlast),
            LoggedAvatarBuff<ApplyBuffEvent>(5_000, effectId: 1_001_331),
            Cast(6_000, Spells.RadiantBlast));

        parser.Events.OfType<UpdateSpellUsableEvent>()
            .ShouldNotContain(e => e.Ability.Id == Spells.RadiantBlast.FSLID && e.UpdateType == UpdateSpellUsableType.EndCooldown && e.Timestamp == 5_000);
    }

    [Fact]
    public async Task RadiantBlastCastsDuringAvatarOfLightStartNoCooldown()
    {
        var parser = await Analyze(
            ApplyBuff(5_000, Spells.AvatarOfLightBuff, PlayerId),
            Cast(6_000, Spells.RadiantBlast),
            Cast(7_000, Spells.RadiantBlast));

        parser.Events.OfType<UpdateSpellUsableEvent>()
            .ShouldNotContain(e => e.Ability.Id == Spells.RadiantBlast.FSLID);
        parser.SpellUsable.ShouldNotBeNull().Drifts.ShouldBeEmpty();
    }

    [Fact]
    public async Task RadiantBlastCastAfterAvatarOfLightRemovalRechargesOverItsBaseCooldown()
    {
        var parser = await Analyze(
            ApplyBuff(5_000, Spells.AvatarOfLightBuff, PlayerId),
            RemoveBuff(14_000, Spells.AvatarOfLightBuff, PlayerId),
            Cast(15_000, Spells.RadiantBlast));

        parser.Events.OfType<UpdateSpellUsableEvent>()
            .Single(e => e.Ability.Id == Spells.RadiantBlast.FSLID && e.UpdateType == UpdateSpellUsableType.EndCooldown)
            .Timestamp.ShouldBe(25_000);
    }

    [Fact]
    public async Task AvatarOfLightEndsTheCooldownsAndCastsDuringItStartNone()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.RadiantBlast),
            Cast(PullStart, Spells.CircleOfLight, PlayerId),
            Cast(PullStart + 1_000, Spells.AvatarOfLight, PlayerId),
            ApplyBuff(PullStart + 1_000, Spells.AvatarOfLightBuff, PlayerId),
            Cast(PullStart + 2_000, Spells.RadiantBlast),
            RemoveBuff(PullStart + 10_000, Spells.AvatarOfLightBuff, PlayerId));

        parser.SpellUsable.ShouldBeOfType<VigourSpellUsable>();
        parser.Events.OfType<UpdateSpellUsableEvent>()
            .Where(e => e.Ability.Id == Spells.RadiantBlast.FSLID && e.UpdateType == UpdateSpellUsableType.BeginCooldown)
            .Select(e => e.Timestamp)
            .ShouldBe([PullStart]);
        parser.SpellUsable.ShouldNotBeNull().IsAvailable(Spells.CircleOfLight.FSLID).ShouldBeTrue();
        parser.SpellUsable.ShouldNotBeNull().IsAvailable(Spells.RadiantBlast.FSLID).ShouldBeTrue();
    }

    private static T LoggedAvatarBuff<T>(int timestamp, int effectId, int? duration = null) where T : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = "Avatar of Light" },
        AbilityGameId = effectId,
        Duration = duration,
    };
}
