using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class LunarlightMarkAnalyzerTests
{
    internal const int PlayerId = 1;
    internal const int TargetA = 20;
    internal const int TargetB = 21;
    internal const int Instance = 1;

    internal static readonly ReportFight Fight =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 20_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task MarkWindow_ClosedByARemoval_CoversTheTimeBetweenTheTwoEvents()
    {
        var analyzer = await Analyze(
            ApplyDebuff(1_000, TargetA),
            RemoveDebuff(6_000, TargetA));

        analyzer.PullDurationMs.ShouldBe(20_000);
        analyzer.MarkedUptimeMs.ShouldBe(5_000);
        analyzer.MarkedUptimePercentage.ShouldBe(25d);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.TargetId.ShouldBe(TargetA);
        window.TargetInstance.ShouldBe(Instance);
        window.StartMs.ShouldBe(1_000);
        window.EndMs.ShouldBe(6_000);
        window.ClosedByRemoval.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkWindow_WithoutARemoval_ClosesAtPullEndAndIsNotAGap()
    {
        var analyzer = await Analyze(ApplyDebuff(1_000, TargetA));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StartMs.ShouldBe(1_000);
        window.EndMs.ShouldBe(20_000);
        window.ClosedByRemoval.ShouldBeFalse();
        analyzer.MarkedUptimeMs.ShouldBe(19_000);
    }

    [Fact]
    public async Task OverlappingWindows_OnTwoTargets_AreUnionedRatherThanAdded()
    {
        var analyzer = await Analyze(
            ApplyDebuff(1_000, TargetA),
            ApplyDebuff(4_000, TargetB),
            RemoveDebuff(6_000, TargetA),
            RemoveDebuff(9_000, TargetB));

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.DistinctMarkedTargets.ShouldBe(2);
        analyzer.MarkedUptimeMs.ShouldBe(8_000);
    }

    [Fact]
    public async Task StackEvents_DoNotMoveTheWindowBoundaries()
    {
        var analyzer = await Analyze(
            ApplyDebuff(1_000, TargetA),
            ApplyDebuffStack(2_000, TargetA, stack: 4),
            RemoveDebuffStack(3_000, TargetA, stack: 3),
            RemoveDebuffStack(4_000, TargetA, stack: 2),
            RemoveDebuff(6_000, TargetA));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StartMs.ShouldBe(1_000);
        window.EndMs.ShouldBe(6_000);
        analyzer.MarkedUptimeMs.ShouldBe(5_000);
        analyzer.TotalApplications.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshDebuff_KeepsTheOpenWindowAndIsNotAnApplication()
    {
        var analyzer = await Analyze(
            ApplyDebuff(1_000, TargetA),
            RefreshDebuff(3_000, TargetA),
            RemoveDebuff(6_000, TargetA));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StartMs.ShouldBe(1_000);
        window.EndMs.ShouldBe(6_000);
        analyzer.TotalApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Applications_AreSplitBetweenTheMarkCastAndTheSecondaryPaths()
    {
        var analyzer = await Analyze(
            MarkCast(1_000),
            ApplyDebuff(1_100, TargetA),
            RemoveDebuff(5_000, TargetA),
            ApplyDebuff(6_000, TargetB));

        analyzer.MarkCasts.ShouldBe(1);
        analyzer.MarkCastApplications.ShouldBe(1);
        analyzer.SecondaryApplications.ShouldBe(1);
        analyzer.TotalApplications.ShouldBe(2);
        analyzer.SecondaryApplicationPercentage.ShouldBe(50d);
    }

    [Fact]
    public async Task Barrage_CountsOnlyTheCastsChannelledIntoAMarkedTarget()
    {
        var analyzer = await Analyze(
            ApplyDebuff(1_000, TargetA),
            BarrageCast(2_000, TargetA),
            BarrageCast(3_000, TargetB));

        analyzer.BarrageCasts.ShouldBe(2);
        analyzer.BarrageCastsOnMarkedTarget.ShouldBe(1);
        analyzer.BarrageOnMarkedPercentage.ShouldBe(50d);
    }

    [Fact]
    public async Task Salvo_AndEruptDamage_AreCountedAsTheMarkPayoff()
    {
        var analyzer = await Analyze(
            ApplyDebuff(1_000, TargetA),
            Damage(1_500, TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 900),
            Damage(1_600, TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 800),
            Damage(1_700, TargetB, Spells.LunarlightMarkAoeDamage.FSLID, amount: 300),
            Damage(1_800, TargetA, Spells.CelestialShotDamage.FSLID, amount: 5_000));

        analyzer.SalvoHits.ShouldBe(2);
        analyzer.EruptHits.ShouldBe(1);
        analyzer.TotalSalvoHits.ShouldBe(3);
    }

    [Fact]
    public async Task Analyze_LunarlightMark_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            ApplyDebuff(1_000, TargetA),
            RemoveDebuff(6_000, TargetA));

        var entry = parser.LunarlightMarkAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.LunarlightMarkAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).LunarlightMarkAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    internal static CastEvent MarkCast(int timestamp, int targetId = TargetA) =>
        Cast(timestamp, targetId, Spells.LunarlightMark.FSLID);

    internal static CastEvent BarrageCast(int timestamp, int targetId) =>
        Cast(timestamp, targetId, Spells.HeartseekerBarrage.FSLID);

    internal static CastEvent Cast(int timestamp, int targetId, int abilityId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = new SpellAbility { FSLID = abilityId, Name = $"Spell {abilityId}" },
    };

    internal static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = MarkDebuffAbility(),
    };

    internal static RefreshDebuffEvent RefreshDebuff(int timestamp, int targetId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = MarkDebuffAbility(),
    };

    internal static RemoveDebuffEvent RemoveDebuff(int timestamp, int targetId, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = MarkDebuffAbility(),
    };

    internal static ApplyDebuffStackEvent ApplyDebuffStack(int timestamp, int targetId, int stack, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Stack = stack,
        Ability = MarkDebuffAbility(),
    };

    internal static RemoveDebuffStackEvent RemoveDebuffStack(int timestamp, int targetId, int stack, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Stack = stack,
        Ability = MarkDebuffAbility(),
    };

    internal static DamageEvent Damage(int timestamp, int targetId, int abilityId, long amount, int instance = Instance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Amount = amount,
        Ability = new SpellAbility { FSLID = abilityId, Name = $"Spell {abilityId}" },
    };

    internal static async Task<(ElarionCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        var result = await parser.Analyze([.. events], PlayerId, Fight);
        return (parser, result);
    }

    private static SpellAbility MarkDebuffAbility() => new()
    {
        FSLID = Spells.LunarlightMarkDebuff.FSLID,
        Name = Spells.LunarlightMarkDebuff.Name,
    };

    private static async Task<LunarlightMarkAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(events);
        return parser.LunarlightMarkAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
