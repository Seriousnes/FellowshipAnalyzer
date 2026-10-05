using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Helena.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class VeteranOfWarAnalyzerTests
{
    [Fact]
    public async Task ReductionAimedAtAnAbilityAlreadyAvailable_IsAllWaste()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.MeasuredStrike));

        analyzer.CooldownReduction.Total.ShouldBe(8_000);
        analyzer.CooldownReduction.Effective.ShouldBe(0);
        analyzer.CooldownReduction.Wasted.ShouldBe(8_000);
        analyzer.CooldownReduction.Efficiency.ShouldBe(0);
    }

    [Fact]
    public async Task ReductionAimedAtARunningCooldown_Applies()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 2_000, Spells.ShieldSlam));

        var toShockwave = Pairing(analyzer, Spells.ShieldSlam.FSLID, Spells.Shockwave.FSLID);

        toShockwave.CooldownReduction.Total.ShouldBe(6_000);
        toShockwave.CooldownReduction.Effective.ShouldBe(6_000);
        toShockwave.CooldownReduction.Wasted.ShouldBe(0);
    }

    [Fact]
    public async Task ReductionLongerThanTheRemainingCooldown_AppliesOnlyWhatWasLeft()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 29_000, Spells.HoldTheLine));

        var toShockwave = Pairing(analyzer, Spells.HoldTheLine.FSLID, Spells.Shockwave.FSLID);

        toShockwave.CooldownReduction.Total.ShouldBe(20_000);
        toShockwave.CooldownReduction.Effective.ShouldBe(2_000);
        toShockwave.CooldownReduction.Wasted.ShouldBe(18_000);
    }

    [Fact]
    public async Task HoldTheLine_GeneratesAgainstAllFourOfItsTargets()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.HoldTheLine));

        var targets = analyzer.Contributions
            .Where(contribution => contribution.SourceSpellId == Spells.HoldTheLine.FSLID.Value)
            .Select(contribution => contribution.TargetSpellId)
            .ToList();

        targets.ShouldBe(
            [
                Spells.ShieldSlam.FSLID.Value,
                Spells.ShieldThrow.FSLID.Value,
                Spells.Shockwave.FSLID.Value,
                Spells.ShieldsUp.FSLID.Value,
            ],
            ignoreOrder: true);
        analyzer.CooldownReduction.Total.ShouldBe(80_000);
    }

    [Fact]
    public async Task AnActiveSpiritAbility_DoublesEveryReductionUnderIt()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.SiegebreakerBuff),
            Cast(PullStart + 2_000, Spells.MeasuredStrike),
            RemoveBuff(PullStart + 3_000, Spells.SiegebreakerBuff),
            Cast(PullStart + 4_000, Spells.MeasuredStrike));

        analyzer.UltimateWasActive.ShouldBeTrue();
        analyzer.CooldownReduction.Total.ShouldBe((8_000 * 2) + 8_000);
    }

    [Fact]
    public async Task SiegebreakerStillActiveWhenAPullStarts_DoublesThatPullsReductions()
    {
        var parser = await AnalyzeIn(
            TwoPullDungeon,
            ApplyBuff(15_000, Spells.SiegebreakerBuff),
            Cast(31_000, Spells.MeasuredStrike),
            RemoveBuff(35_000, Spells.SiegebreakerBuff));

        var secondPull = parser.VeteranOfWarAnalyzers.Single(entry => entry.Pull.StartTime == 30_000).Analyzer;

        secondPull.UltimateWasActive.ShouldBeTrue();
        secondPull.CooldownReduction.Total.ShouldBe(8_000 * 2);
    }

    [Fact]
    public async Task AHoldTheLineCastBetweenPulls_StillShortensTheCooldownsItNames()
    {
        var parser = await HelenaAnalysisFixture.Analyze(
            Cast(PullStart - 500, Spells.Shockwave),
            Cast(PullStart - 400, Spells.HoldTheLine));

        ShockwaveReadyAt(parser).ShouldBe(PullStart + 9_500);
        parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer.CooldownReduction.Total.ShouldBe(0);
    }

    [Fact]
    public async Task BySource_CountsCastsRatherThanSourceToTargetPairings()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.HoldTheLine),
            Cast(PullStart + 2_000, Spells.HoldTheLine));

        analyzer.BySource
            .Single(contribution => contribution.SourceSpellId == Spells.HoldTheLine.FSLID.Value)
            .Events.ShouldBe(2);
    }

    [Fact]
    public void TheComboTable_HoldsTwiceTheTooltipFigureForEveryPairing()
    {
        var combos = VeteranOfWar.Combos.ToLookup(combo => combo.SourceSpellId);

        combos[Spells.MeasuredStrike.FSLID].Select(combo => combo.ReductionMs).ShouldAllBe(ms => ms == 4_000);
        combos[Spells.PowerStrike.FSLID].Select(combo => combo.ReductionMs).ShouldAllBe(ms => ms == 4_000);
        combos[Spells.ShieldSlam.FSLID].ShouldHaveSingleItem().ReductionMs.ShouldBe(6_000);
        combos[Spells.ShieldThrow.FSLID].ShouldHaveSingleItem().ReductionMs.ShouldBe(6_000);
        combos[Spells.Shockwave.FSLID].ShouldHaveSingleItem().ReductionMs.ShouldBe(12_000);
        combos[Spells.HoldTheLine.FSLID].Select(combo => combo.ReductionMs).ShouldAllBe(ms => ms == 20_000);
        VeteranOfWar.ActiveUltimateScaler.ShouldBe(2.0);
    }

    [Fact]
    public async Task AHoldTheLineCast_RecordsHowLongEachTargetHadBeenAvailable()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 3_000, Spells.HoldTheLine));

        var cast = analyzer.HoldTheLineCasts.ShouldHaveSingleItem();
        cast.Timestamp.ShouldBe(PullStart + 3_000);
        cast.Targets.Select(target => target.SpellId).ShouldBe(VeteranOfWar.HoldTheLineTargets);

        Target(cast, Spells.Shockwave).AvailableForMs.ShouldBeNull();
        Target(cast, Spells.ShieldThrow).AvailableForMs.ShouldBe(3_000);
        Target(cast, Spells.ShieldsUp).AvailableForMs.ShouldBe(3_000);
    }

    [Fact]
    public async Task ATargetTheSameCastMakesAvailable_IsStillRecordedAsRecharging()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 26_000, Spells.HoldTheLine));

        var cast = analyzer.HoldTheLineCasts.ShouldHaveSingleItem();

        Target(cast, Spells.Shockwave).WasAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task ShieldSlamWithOneChargeSpent_IsAvailableAtTheCast()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.ShieldSlam),
            Cast(PullStart + 2_000, Spells.HoldTheLine));

        var cast = analyzer.HoldTheLineCasts.ShouldHaveSingleItem();

        Target(cast, Spells.ShieldSlam).WasAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task ShieldSlamWithEveryChargeSpent_IsNotAvailableAtTheCast()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.ShieldSlam),
            Cast(PullStart + 1_500, Spells.ShieldSlam),
            Cast(PullStart + 2_000, Spells.HoldTheLine));

        var cast = analyzer.HoldTheLineCasts.ShouldHaveSingleItem();

        Target(cast, Spells.ShieldSlam).WasAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task ShieldSlamNotCast_IsAvailableAtTheCast()
    {
        var analyzer = await Analyze(Cast(PullStart + 2_000, Spells.HoldTheLine));

        var cast = analyzer.HoldTheLineCasts.ShouldHaveSingleItem();

        Target(cast, Spells.ShieldSlam).AvailableForMs.ShouldBe(2_000);
    }

    [Fact]
    public async Task EveryHoldTheLineTarget_HasItsUsabilityTracked()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.HoldTheLine));

        analyzer.HoldTheLineCasts
            .ShouldHaveSingleItem()
            .Targets.Select(target => target.SpellId)
            .ShouldBe(VeteranOfWar.HoldTheLineTargets);
        VeteranOfWar.HoldTheLineTargets.ShouldBe(
            [
                Spells.ShieldSlam.FSLID.Value,
                Spells.ShieldThrow.FSLID.Value,
                Spells.Shockwave.FSLID.Value,
                Spells.ShieldsUp.FSLID.Value,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task AHoldTheLineCast_RecordsWhatEachTargetsReductionGenerated()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 29_000, Spells.HoldTheLine));

        var cast = analyzer.HoldTheLineCasts.ShouldHaveSingleItem();

        Target(cast, Spells.Shockwave).CooldownReduction.ShouldBe(new CooldownReductionResult(20_000, 2_000));
        Target(cast, Spells.ShieldsUp).CooldownReduction.ShouldBe(new CooldownReductionResult(20_000, 0));
    }

    [Fact]
    public async Task ATargetTheCastItselfMakesAvailable_KeepsItsReductionEffective()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 26_000, Spells.HoldTheLine));

        var shockwave = Target(analyzer.HoldTheLineCasts.ShouldHaveSingleItem(), Spells.Shockwave);

        shockwave.WasAvailable.ShouldBeFalse();
        shockwave.CooldownReduction.Effective.ShouldBe(5_000);
    }

    [Fact]
    public void ReductionTargets_NameEveryReducedAbilityOnceInComboTableOrder()
    {
        VeteranOfWar.ReductionTargets.ShouldBe(
            [
                Spells.ShieldSlam.FSLID.Value,
                Spells.ShieldThrow.FSLID.Value,
                Spells.Shockwave.FSLID.Value,
                Spells.ShieldsUp.FSLID.Value,
            ]);
    }

    /// <summary>Two pulls with a gap between them, so state can be carried from the first into the second.</summary>
    private static ReportDungeon TwoPullDungeon { get; } = new(
        Id: 0, Name: "Two pulls", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 62_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null,
        InProgress: false,
        DungeonPulls:
        [
            new DungeonPull(1, 1, true, 1_000, 20_000, "First", null),
            new DungeonPull(2, 1, true, 30_000, 61_000, "Second", null),
        ]);

    private static int ShockwaveReadyAt(HelenaCombatLogParser parser) =>
        parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Last(update => update.Ability.Id == Spells.Shockwave.FSLID
                && update.UpdateType == UpdateSpellUsableType.EndCooldown)
            .Timestamp;

    private static HoldTheLineTarget Target(HoldTheLineCast cast, Core.Common.Spells.Spell spell) =>
        cast.Targets.Single(target => target.SpellId == spell.FSLID.Value);

    private static CooldownContribution Pairing(VeteranOfWarAnalyzer analyzer, int source, int target) =>
        analyzer.Contributions.Single(contribution =>
            contribution.SourceSpellId == source && contribution.TargetSpellId == target);

    private static async Task<VeteranOfWarAnalyzer> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
