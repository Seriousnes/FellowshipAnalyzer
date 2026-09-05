using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;
using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class FocusedWrathAnalyzerTests
{
    private const int PlayerId = 7;
    private const int DungeonEnd = 300_000;

    private static readonly int SelfBuff = TariqSpells.FocusedWrathSelfBuff.FSLID;
    private static readonly int FocusedWrath = TariqSpells.FocusedWrath.FSLID;
    private static readonly int HammerStorm = TariqSpells.HammerStorm.FSLID;
    private static readonly int SkullCrusher = TariqSpells.SkullCrusher.FSLID;
    private static readonly int CullingStrike = TariqSpells.CullingStrike.FSLID;

    /// <summary>
    /// Each charge that leaves the buff is recorded on its own, in the order it left, with the
    /// spender cast that took it and how that cast was rated on this pull.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_NamesTheSpenderEachChargeWentTo()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, FocusedWrath),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
            Cast(1_300, HammerStorm),
            RemoveStack(1_300, SelfBuff, stacks: 1),
            Cast(1_800, SkullCrusher),
            Remove(1_800, SelfBuff),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        entry.Pull.Targets.ShouldBe(PullKind.Single);

        var analyzer = entry.Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.OpenedAt.ShouldBe(1_100);
        window.ClosedAt.ShouldBe(1_800);
        window.FromCast.ShouldBeTrue();
        window.ClippedByPullEnd.ShouldBeFalse();
        window.ChargesGranted.ShouldBe(2);
        window.ChargesConsumed.ShouldBe(2);
        window.ChargesWasted.ShouldBe(0);

        window.Consumptions[0].Timestamp.ShouldBe(1_300);
        window.Consumptions[0].SpenderSpellId.ShouldBe(HammerStorm);
        window.Consumptions[0].Choice.ShouldBe(SpenderChoice.Wrong);
        window.Consumptions[1].Timestamp.ShouldBe(1_800);
        window.Consumptions[1].SpenderSpellId.ShouldBe(SkullCrusher);
        window.Consumptions[1].Choice.ShouldBe(SpenderChoice.Correct);

        window.CorrectSpenders.ShouldBe(1);
        window.WrongSpenders.ShouldBe(1);
        window.RatedSpenders.ShouldBe(2);

        analyzer.WindowCount.ShouldBe(1);
        analyzer.HammerStormConsumptions.ShouldBe(1);
        analyzer.SkullCrusherConsumptions.ShouldBe(1);
        analyzer.UnratedConsumptions.ShouldBe(0);
    }

    /// <summary>
    /// A Focused Wrath cast opens the buff with a stack application at the application's own
    /// timestamp, so it grants <see cref="FocusedWrathAnalyzer.StacksPerCast"/> charges. A Leap Smash
    /// proc opens it bare, with one. The shared timestamp is what separates the two.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_ReadsACastBuffFromTheStackSharingTheApplicationsTimestamp()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Cast(1_200, SkullCrusher),
            Remove(1_200, SelfBuff),
            Cast(50_000, FocusedWrath),
            Apply(50_100, SelfBuff),
            Stack(50_100, SelfBuff, stacks: 2),
            Cast(50_400, HammerStorm),
            RemoveStack(50_400, SelfBuff, stacks: 1),
            Cast(50_700, HammerStorm),
            Remove(50_700, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.WindowCount.ShouldBe(2);

        analyzer.Windows[0].FromCast.ShouldBeFalse();
        analyzer.Windows[0].ChargesGranted.ShouldBe(1);
        analyzer.Windows[1].FromCast.ShouldBeTrue();
        analyzer.Windows[1].ChargesGranted.ShouldBe(FocusedWrathAnalyzer.StacksPerCast);

        analyzer.ChargesGranted.ShouldBe(3);
        analyzer.ChargesFromCast.ShouldBe(FocusedWrathAnalyzer.StacksPerCast);
        analyzer.ChargesFromProc.ShouldBe(1);
    }

    /// <summary>
    /// The removal that ends the buff is its last charge, not the buff entire: a one-charge proc buff
    /// spent by that removal counts one, and a two-charge cast buff counts the stack removal before it
    /// as well.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_CountsTheFinalRemovalAsOneChargeRatherThanTheWholeBuff()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Cast(1_200, SkullCrusher),
            Remove(1_200, SelfBuff),
            Cast(20_000, FocusedWrath),
            Apply(20_100, SelfBuff),
            Stack(20_100, SelfBuff, stacks: 2),
            Cast(20_300, SkullCrusher),
            RemoveStack(20_300, SelfBuff, stacks: 1),
            Cast(20_600, SkullCrusher),
            Remove(20_600, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Windows[0].ChargesGranted.ShouldBe(1);
        analyzer.Windows[0].ChargesConsumed.ShouldBe(1);
        analyzer.Windows[1].ChargesGranted.ShouldBe(2);
        analyzer.Windows[1].ChargesConsumed.ShouldBe(2);
        analyzer.Windows[1].Consumptions.Count.ShouldBe(2);

        analyzer.ChargesGranted.ShouldBe(3);
        analyzer.ChargesConsumed.ShouldBe(3);
        analyzer.ChargesWasted.ShouldBe(0);
        analyzer.SkullCrusherConsumptions.ShouldBe(3);
    }

    /// <summary>
    /// Culling Strike is not a Focused Wrath spender, so a Culling Strike cast inside an active buff
    /// is never matched to the charge that left it. The charge is still counted as spent, with no
    /// spender named and no rating.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_LeavesAChargeUnratedWhenOnlyACullingStrikeWasCastInsideTheBuff()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Cast(5_000, CullingStrike),
            Remove(5_000, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.ChargesConsumed.ShouldBe(1);
        window.ChargesWasted.ShouldBe(0);
        window.RatedSpenders.ShouldBe(0);

        var consumption = window.Consumptions.ShouldHaveSingleItem();
        consumption.Choice.ShouldBe(SpenderChoice.Unrated);
        consumption.SpenderSpellId.ShouldBeNull();
        consumption.Timestamp.ShouldBeNull();

        analyzer.UnratedConsumptions.ShouldBe(1);
        analyzer.HammerStormConsumptions.ShouldBe(0);
        analyzer.SkullCrusherConsumptions.ShouldBe(0);
        analyzer.CorrectSpenders.ShouldBe(0);
        analyzer.WrongSpenders.ShouldBe(0);
    }

    /// <summary>
    /// A spender cast is matched to a charge up to <see cref="FocusedWrathAnalyzer.ConsumptionGraceMs"/>
    /// after the charge left the buff, so a cast logged just past the removal still names it.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_MatchesASpenderCastWithinTheGraceAfterTheChargeLeft()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Remove(5_000, SelfBuff),
            Cast(5_000 + FocusedWrathAnalyzer.ConsumptionGraceMs, SkullCrusher),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        entry.Pull.Targets.ShouldBe(PullKind.Single);

        var analyzer = entry.Analyzer;
        var consumption = analyzer.Windows.ShouldHaveSingleItem().Consumptions.ShouldHaveSingleItem();

        consumption.Timestamp.ShouldBe(5_000 + FocusedWrathAnalyzer.ConsumptionGraceMs);
        consumption.SpenderSpellId.ShouldBe(SkullCrusher);
        consumption.Choice.ShouldBe(SpenderChoice.Correct);

        analyzer.SkullCrusherConsumptions.ShouldBe(1);
    }

    /// <summary>
    /// A buff that reaches <see cref="FocusedWrathAnalyzer.BuffDurationMs"/> expired rather than being
    /// spent, so the charge still available at the removal is counted as wasted instead of as the
    /// consumption that ended the buff.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_CountsAChargeStillAvailableWhenTheBuffRanItsFullDurationAsWasted()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, FocusedWrath),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
            Cast(2_000, SkullCrusher),
            RemoveStack(2_000, SelfBuff, stacks: 1),
            Remove(1_100 + FocusedWrathAnalyzer.BuffDurationMs, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.DurationMs.ShouldBe(FocusedWrathAnalyzer.BuffDurationMs);
        window.ClippedByPullEnd.ShouldBeFalse();
        window.ChargesGranted.ShouldBe(2);
        window.ChargesConsumed.ShouldBe(1);
        window.ChargesWasted.ShouldBe(1);
        window.Consumptions.ShouldHaveSingleItem().SpenderSpellId.ShouldBe(SkullCrusher);

        analyzer.ChargesWasted.ShouldBe(1);
    }

    /// <summary>
    /// A fresh application closes the buff already active. The charges left on the closed buff are
    /// counted as wasted rather than added to the new one, which opens with a single charge of its own.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_CountsTheChargesLeftOnABuffAFreshApplicationClosed()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, FocusedWrath),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
            Apply(5_000, SelfBuff),
            Cast(6_000, SkullCrusher),
            Remove(6_000, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.WindowCount.ShouldBe(2);

        var closed = analyzer.Windows[0];
        closed.ClosedAt.ShouldBe(5_000);
        closed.DurationMs.ShouldBeLessThan(FocusedWrathAnalyzer.BuffDurationMs);
        closed.ClippedByPullEnd.ShouldBeFalse();
        closed.ChargesGranted.ShouldBe(2);
        closed.ChargesConsumed.ShouldBe(0);
        closed.ChargesWasted.ShouldBe(2);

        var opened = analyzer.Windows[1];
        opened.OpenedAt.ShouldBe(5_000);
        opened.FromCast.ShouldBeFalse();
        opened.ChargesGranted.ShouldBe(1);
        opened.ChargesConsumed.ShouldBe(1);

        analyzer.ChargesGranted.ShouldBe(3);
        analyzer.ChargesConsumed.ShouldBe(1);
        analyzer.ChargesWasted.ShouldBe(2);
    }

    /// <summary>
    /// What was still available on a buff the pull end cut short is an inference rather than a
    /// measurement, so a clipped buff contributes nothing to the waste count.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_LeavesChargesAvailableAtPullEndOutOfTheWasteCount()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, FocusedWrath),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.ClippedByPullEnd.ShouldBeTrue();
        window.ClosedAt.ShouldBe(entry.Pull.EndTime);
        window.ChargesGranted.ShouldBe(2);
        window.ChargesConsumed.ShouldBe(0);
        window.ChargesWasted.ShouldBe(0);

        analyzer.ChargesWasted.ShouldBe(0);
    }

    /// <summary>
    /// A Leap Smash proc applied to a cast buff adds a third charge, and all three can be spent. The
    /// charges a buff granted are what a spend count is read against, rather
    /// than <see cref="FocusedWrathAnalyzer.MaxStackLimit"/>.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_CountsAProcTopUpAsAnExtraChargeOnACastBuff()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, FocusedWrath),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
            Stack(1_200, SelfBuff, stacks: 3),
            Cast(1_400, SkullCrusher),
            RemoveStack(1_400, SelfBuff, stacks: 2),
            Cast(1_700, SkullCrusher),
            RemoveStack(1_700, SelfBuff, stacks: 1),
            Cast(2_000, SkullCrusher),
            Remove(2_000, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.FromCast.ShouldBeTrue();
        window.ChargesGranted.ShouldBe(3);
        window.ChargesConsumed.ShouldBe(3);
        window.ChargesWasted.ShouldBe(0);

        analyzer.ChargesGranted.ShouldBe(3);
        analyzer.ChargesFromCast.ShouldBe(FocusedWrathAnalyzer.StacksPerCast);
        analyzer.ChargesFromProc.ShouldBe(1);
        analyzer.SkullCrusherConsumptions.ShouldBe(3);
    }

    /// <summary>
    /// Charges accumulate over the buff's life, so a proc topping it up after a charge was spent grants
    /// a third while the stack count the log reports never passes two. Report
    /// <c>a:NcqHDKzamL7n6YFv</c> has this sequence three times.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_CountsEveryTopUpWhenAChargeWasSpentBetweenThem()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Stack(1_500, SelfBuff, stacks: 2),
            Cast(2_000, SkullCrusher),
            RemoveStack(2_000, SelfBuff, stacks: 1),
            Stack(2_500, SelfBuff, stacks: 2),
            Cast(3_000, SkullCrusher),
            RemoveStack(3_000, SelfBuff, stacks: 1),
            Cast(3_500, SkullCrusher),
            Remove(3_500, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.FromCast.ShouldBeFalse();
        window.ChargesGranted.ShouldBe(3);
        window.ChargesConsumed.ShouldBe(3);
        window.ChargesWasted.ShouldBe(0);

        analyzer.ChargesFromCast.ShouldBe(0);
        analyzer.ChargesFromProc.ShouldBe(3);
    }

    /// <summary>
    /// Without Schism the spender a charge should go to follows the pull shape: Skull Crusher on a boss
    /// pull, Hammer Storm on a trash pull. The same Skull Crusher cast is correct on one and wrong on
    /// the other.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_AimsAChargeAtSkullCrusherOnABossPullAndHammerStormOnATrashPull()
    {
        var (boss, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(SkullCrusherSpend());
        var trash = await AnalyzeTrashAsync(SkullCrusherSpend());

        var bossEntry = boss.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        bossEntry.Pull.Targets.ShouldBe(PullKind.Single);
        bossEntry.Analyzer.SchismTalented.ShouldBeFalse();
        bossEntry.Analyzer.PreferredSpenderId.ShouldBe(SkullCrusher);
        bossEntry.Analyzer.Windows.ShouldHaveSingleItem().PreferredSpenderId.ShouldBe(SkullCrusher);
        bossEntry.Analyzer.CorrectSpenders.ShouldBe(1);
        bossEntry.Analyzer.WrongSpenders.ShouldBe(0);

        var trashEntry = trash.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        trashEntry.Pull.Targets.ShouldBe(PullKind.Multi);
        trashEntry.Analyzer.SchismTalented.ShouldBeFalse();
        trashEntry.Analyzer.PreferredSpenderId.ShouldBe(HammerStorm);
        trashEntry.Analyzer.Windows.ShouldHaveSingleItem().PreferredSpenderId.ShouldBe(HammerStorm);
        trashEntry.Analyzer.CorrectSpenders.ShouldBe(0);
        trashEntry.Analyzer.WrongSpenders.ShouldBe(1);
    }

    /// <summary>
    /// Schism makes Hammer Storm the spender to aim a charge at on every pull shape, so a Hammer Storm
    /// on a boss pull is correct where it would otherwise be wrong.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_AimsAChargeAtHammerStormOnABossPullWhenSchismIsTalented()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            CombatantInfo(schism: true),
            Apply(1_000, SelfBuff),
            Cast(1_200, HammerStorm),
            Remove(1_200, SelfBuff),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        entry.Pull.Targets.ShouldBe(PullKind.Single);

        var analyzer = entry.Analyzer;

        analyzer.SchismTalented.ShouldBeTrue();
        analyzer.PreferredSpenderId.ShouldBe(HammerStorm);
        analyzer.CorrectSpenders.ShouldBe(1);
        analyzer.WrongSpenders.ShouldBe(0);
        analyzer.HammerStormConsumptions.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_ExposesPerPullReadPaths()
    {
        var (parser, _) = await ThunderCallAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, FocusedWrath),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
            Cast(1_500, SkullCrusher),
            RemoveStack(1_500, SelfBuff, stacks: 1),
            Cast(1_900, SkullCrusher),
            Remove(1_900, SelfBuff),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.FocusedWrathAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).FocusedWrathAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static List<Event> SkullCrusherSpend() =>
    [
        Apply(1_000, SelfBuff),
        Cast(1_200, SkullCrusher),
        Remove(1_200, SelfBuff),
    ];

    /// <summary>
    /// Runs the pipeline over a dungeon with no encounter, which <c>PullBookendNormalizer</c> classifies
    /// as a trash pull. The shared boss helper cannot express that shape, and the spender a charge
    /// should go to turns on it.
    /// </summary>
    private static async Task<TariqCombatLogParser> AnalyzeTrashAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var dungeon = new ReportDungeon(0, "Trash", 0, true, 0, DungeonEnd, null, null, null);
        await parser.Analyze(events, playerId: PlayerId, dungeon: dungeon);
        return parser;
    }

    private static CastEvent Cast(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.CastWithoutResources(timestamp, abilityId);

    private static ApplyBuffEvent Apply(int timestamp, int abilityId) =>
        ThunderCallAnalyzerTests.Apply(timestamp, abilityId);

    private static RemoveBuffEvent Remove(int timestamp, int abilityId) =>
        ThunderCallAnalyzerTests.Remove(timestamp, abilityId);

    private static ApplyBuffStackEvent Stack(int timestamp, int abilityId, int stacks)
    {
        var stackEvent = FuryEconomyAnalyzerTests.Buff<ApplyBuffStackEvent>(timestamp, abilityId);
        stackEvent.Stack = stacks;
        return stackEvent;
    }

    private static RemoveBuffStackEvent RemoveStack(int timestamp, int abilityId, int stacks)
    {
        var stackEvent = FuryEconomyAnalyzerTests.Buff<RemoveBuffStackEvent>(timestamp, abilityId);
        stackEvent.Stack = stacks;
        return stackEvent;
    }

    private static CombatantInfoEvent CombatantInfo(bool schism) => new()
    {
        SourceId = PlayerId,
        Talents = schism ? [new TalentInfo { Id = 2_000_000 + TariqTalents.Schism }] : [],
    };
}
