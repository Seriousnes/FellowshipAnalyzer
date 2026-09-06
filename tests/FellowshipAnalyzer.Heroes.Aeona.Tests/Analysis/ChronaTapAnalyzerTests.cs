using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises Chrona Tap across the report. <c>ResourceNormalizer</c> divides every resource by 100 before
/// dispatch, mana included, so <see cref="MaxMana"/> is the pool the analyzer sees.
/// </summary>
public sealed class ChronaTapAnalyzerTests
{
    private const int PlayerId = 7;
    private const int RawMaxMana = 165_600;
    private const int MaxMana = RawMaxMana / 100;
    private const int RawManaWellBelowTheCap = 100_000;
    private const int RoomLeftBelowTheCap = 10;
    private const int RawManaNearTheCap = (MaxMana - RoomLeftBelowTheCap) * 100;
    private const int PullEnd = 60_000;

    private static readonly int ManaPerStack = (int)Math.Round(0.013 * MaxMana);

    [Fact]
    public async Task WithoutTheTalent_TheAnalyzerDoesNotRun()
    {
        var parser = await AnalyzeParser([Combatant(), Applied(1_000), Removed(2_000)]);

        parser.ChronaTap.ShouldBeNull();
    }

    [Fact]
    public async Task ManaReturned_CountsEveryStackHeldWhenTheEffectEnds()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 2),
            Stacked(3_000, 3),
            Removed(4_000));

        analyzer.ManaPerStack.ShouldBe(ManaPerStack);
        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task EachStackThatExpiresOnItsOwn_ReturnsItsOwnMana()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 3),
            StackRemoved(3_000, 2),
            StackRemoved(4_000, 1),
            Removed(5_000));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task SeveralStacksLostAtOnce_ReturnEachOfTheirShares()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 4),
            StackRemoved(3_000, 1));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task StacksReportedAboveTheCap_ReturnTheCapsWorthOfMana()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, ChronaTapAnalyzer.MaximumStacks + 2),
            Removed(3_000));

        analyzer.ManaReturned.ShouldBe(ChronaTapAnalyzer.MaximumStacks * ManaPerStack);
    }

    [Fact]
    public async Task AnExpiryWithRoomInThePool_LosesNothingAtTheCap()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 3),
            Removed(3_000));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
        analyzer.ManaLostAtCap.ShouldBe(0);
    }

    [Fact]
    public async Task AnExpiryOnAFullPool_LosesEveryStackShare()
    {
        var analyzer = await Analyze(
            Applied(1_000, RawMaxMana),
            Stacked(2_000, 3, RawMaxMana),
            Removed(3_000, RawMaxMana));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
        analyzer.ManaLostAtCap.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task AnExpiryReturningMoreThanThePoolHoldsRoomFor_LosesTheOverflow()
    {
        var analyzer = await Analyze(
            Applied(1_000, RawManaNearTheCap),
            Stacked(2_000, 3, RawManaNearTheCap),
            Removed(3_000, RawManaNearTheCap));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
        analyzer.ManaLostAtCap.ShouldBe((3 * ManaPerStack) - RoomLeftBelowTheCap);
    }

    [Fact]
    public async Task TheStackCapMatchesTheExport() =>
        ChronaTapAnalyzer.MaximumStacks.ShouldBe(10);

    [Fact]
    public async Task ThePerStackShareComesFromTheTalentRecord()
    {
        var generation = Talents.ChronaTap.ResourceGeneration.ShouldNotBeNull();

        generation.Amount.ShouldBe(0.013, 0.000001);
        generation.Resource.ShouldBe(ResourceTypes.Mana);

        var analyzer = await Analyze(Applied(1_000), Removed(2_000));
        analyzer.ManaPerStack.ShouldBe(ManaPerStack);
    }

    [Fact]
    public async Task NothingRecorded_ReadsZeroWithoutFailing()
    {
        var analyzer = await Analyze();

        analyzer.ManaReturned.ShouldBe(0);
        analyzer.ManaLostAtCap.ShouldBe(0);
    }

    private static CombatantInfoEvent Combatant(params int[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(id => new TalentInfo { Id = id })],
    };

    private static ApplyBuffEvent Applied(int timestamp, int rawMana = RawManaWellBelowTheCap) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(rawMana),
    };

    private static ApplyBuffStackEvent Stacked(int timestamp, int stack, int rawMana = RawManaWellBelowTheCap) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(rawMana),
    };

    private static RemoveBuffStackEvent StackRemoved(int timestamp, int stack, int rawMana = RawManaWellBelowTheCap) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(rawMana),
    };

    private static RemoveBuffEvent Removed(int timestamp, int rawMana = RawManaWellBelowTheCap) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(rawMana),
    };

    private static ActorResources ManaSnapshot(int rawMana) => new()
    {
        HitPoints = 20_000,
        MaxHitPoints = 30_000,
        Resources = [new ClassResource { Type = ResourceTypes.Mana, Amount = rawMana, Max = RawMaxMana }],
    };

    private static async Task<ChronaTapAnalyzer> Analyze(params Event[] events)
    {
        var parser = await AnalyzeParser([Combatant(AeonaTalents.ChronaTap), .. events]);

        return parser.ChronaTap.ShouldNotBeNull();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeParser(Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "Boss", 1, true, 0, PullEnd, null, null, null));
        return parser;
    }
}
