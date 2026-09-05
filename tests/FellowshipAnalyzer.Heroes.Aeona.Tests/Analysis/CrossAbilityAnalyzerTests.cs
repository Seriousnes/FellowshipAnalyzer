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

using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises the figures that belong to no single ability. Chrona and mana are written at the raw log
/// scale because <c>ResourceNormalizer</c> divides every resource by 100 before dispatch.
/// </summary>
public sealed class CrossAbilityAnalyzerTests
{
    private const int PlayerId = 7;
    private const int TankId = 9;
    private const int EnemyId = 100;
    private const int DungeonEndTime = 60_000;
    private const int RawChronaCap = 10_000;
    private const int RawManaCap = 165_600;

    private static readonly List<ReportActor> Party =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
    ];

    [Fact]
    public async Task ChronaGeneratedAndOvercapped_SumOverEveryPull()
    {
        var analyzer = await Analyze(
            TimeShardDamage(1_000, rawChrona: 2_000),
            TimeShardDamage(2_000, rawChrona: 2_600),
            TimeShardDamage(3_000, rawChrona: 9_900),
            TimeShardDamage(4_000, rawChrona: RawChronaCap));

        analyzer.ChronaGenerated.ShouldBe(6 + 73 + 1);
        analyzer.ChronaOvercapped.ShouldBe(5);
        analyzer.ChronaOvercapShare.ShouldBe(5 / 85d, 0.0001);
    }

    [Fact]
    public async Task ManaMaximum_ComesFromTheTrackedPool()
    {
        var analyzer = await Analyze(TimeShardDamage(1_000, rawChrona: 2_000, rawMana: 100_000));

        analyzer.ManaMaximum.ShouldBe(RawManaCap / 100);
    }

    [Fact]
    public async Task ManaArrivingInsideTheWindow_IsAttributedToACleanse()
    {
        var analyzer = await Analyze(
            CleanseCast(5_000),
            ManaGain(5_200, rawMana: 100_000),
            ManaGain(5_400, rawMana: 112_420));

        var cleanse = analyzer.CleanseManaReturns.ShouldHaveSingleItem();
        cleanse.Timestamp.ShouldBe(5_000);
        cleanse.Ability.ShouldBe(Spells.AmendFate.FSLID);
        cleanse.Mana.ShouldBe(124);
        analyzer.ManaFromCleansing.ShouldBe(124);
    }

    [Fact]
    public async Task ManaArrivingOutsideTheWindow_IsNotAttributedToACleanse()
    {
        var analyzer = await Analyze(
            CleanseCast(5_000),
            ManaGain(5_200, rawMana: 100_000),
            ManaGain(5_000 + CrossAbilityAnalyzer.CleanseReturnWindowMs + 500, rawMana: 112_420));

        analyzer.CleanseManaReturns.ShouldBeEmpty();
        analyzer.ManaFromCleansing.ShouldBe(0);
    }

    [Fact]
    public async Task ASecondCleanseCutsTheFirstWindowShort()
    {
        var analyzer = await Analyze(
            CleanseCast(5_000),
            ManaGain(5_100, rawMana: 100_000),
            CleanseCast(5_300),
            ManaGain(5_400, rawMana: 112_420));

        analyzer.CleanseManaReturns.Count.ShouldBe(1);
        analyzer.CleanseManaReturns[0].Timestamp.ShouldBe(5_300);
    }

    [Fact]
    public async Task SkyboltAtMaximumCharges_ComesFromThePerPullAnalyzers()
    {
        var analyzer = await Analyze(SkyboltCast(10_000));

        analyzer.SkyboltWasCast.ShouldBeTrue();
        analyzer.SkyboltTimeAtMaxChargesMs.ShouldBe(40_000);
        analyzer.SkyboltTimeAtMaxChargesShare.ShouldBe(40_000 / (double)DungeonEndTime, 0.0001);
    }

    [Fact]
    public async Task WithoutASkyboltCast_SkyboltWasNotCast()
    {
        var analyzer = await Analyze(TimeShardDamage(1_000, rawChrona: 2_000));

        analyzer.SkyboltWasCast.ShouldBeFalse();
    }

    [Fact]
    public async Task UnfoldingDoomOverwritten_ComesFromThePerPullAnalyzers()
    {
        var analyzer = await Analyze(
            DoomApplied(1_000),
            DoomRefreshed(6_000),
            DoomRemoved(20_000));

        analyzer.UnfoldingDoomReapplications.ShouldBe(1);
        analyzer.UnfoldingDoomOverwrittenMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task WithNothingToReport_NoCardIsRendered()
    {
        var analyzer = await Analyze();

        analyzer.ChronaGenerated.ShouldBe(0);
        analyzer.ChronaOvercapped.ShouldBe(0);
        analyzer.ManaFromCleansing.ShouldBe(0);
        analyzer.SkyboltWasCast.ShouldBeFalse();
        analyzer.UnfoldingDoomOverwrittenMs.ShouldBe(0);
        analyzer.Statistic.ShouldBeNull();
    }

    [Fact]
    public async Task WithFiguresToReport_TheCardIsRendered()
    {
        var analyzer = await Analyze(
            TimeShardDamage(1_000, rawChrona: 2_000),
            TimeShardDamage(2_000, rawChrona: 2_600));

        analyzer.Statistic.ShouldNotBeNull();
    }

    private static DamageEvent TimeShardDamage(int timestamp, int rawChrona, int? rawMana = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Amount = 500,
        Ability = new Ability { Id = Spells.TimeShard.FSLID },
        SourceResources = Resources(rawChrona, rawMana),
    };

    private static HealEvent ManaGain(int timestamp, int rawMana) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TankId,
        Amount = 400,
        Ability = new Ability { Id = CoreItems.RestoreMana.FSLID },
        SourceResources = Resources(null, rawMana),
    };

    private static CastEvent CleanseCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TankId,
        Ability = new Ability { Id = Spells.AmendFate.FSLID },
    };

    private static CastEvent SkyboltCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = CoreItems.TwilightSkybolt.FSLID },
    };

    private static ApplyDebuffEvent DoomApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = Spells.UnfoldingDoomDebuff.FSLID },
    };

    private static RefreshDebuffEvent DoomRefreshed(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = Spells.UnfoldingDoomDebuff.FSLID },
    };

    private static RemoveDebuffEvent DoomRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = Spells.UnfoldingDoomDebuff.FSLID },
    };

    private static ActorResources Resources(int? rawChrona, int? rawMana)
    {
        var resources = new List<ClassResource>();
        if (rawChrona is { } chrona)
            resources.Add(new ClassResource { Type = ResourceTypes.Primary, Amount = chrona, Max = RawChronaCap });
        if (rawMana is { } mana)
            resources.Add(new ClassResource { Type = ResourceTypes.Mana, Amount = mana, Max = RawManaCap });

        return new ActorResources { HitPoints = 20_000, MaxHitPoints = 40_000, Resources = resources };
    }

    private static async Task<CrossAbilityAnalyzer> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        parser.Actors = Party;
        await parser.Analyze(
            [.. events],
            PlayerId,
            new ReportDungeon(0, "Boss", 1, true, 0, DungeonEndTime, null, null, null));

        return parser.CrossAbility.ShouldNotBeNull();
    }
}
