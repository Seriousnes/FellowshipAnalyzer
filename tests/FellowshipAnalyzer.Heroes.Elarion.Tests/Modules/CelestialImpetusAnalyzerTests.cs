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

public sealed class CelestialImpetusAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 20_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task ProcRemoved_JustAfterACelestialShot_IsConsumedAndTheCastCountsAsSpent()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            CelestialShot(2_000),
            RemoveBuff(2_050));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);
        analyzer.CelestialShotCasts.ShouldBe(1);
        analyzer.CelestialShotCastsWithImpetus.ShouldBe(1);
        analyzer.CelestialShotCastsWithoutImpetus.ShouldBe(0);
        analyzer.CelestialShotWithImpetusPercentage.ShouldBe(100d);
        analyzer.PullDurationMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task CelestialShot_WithNoProcBanked_CountsAsABareCast()
    {
        var analyzer = await Analyze(CelestialShot(1_000));

        analyzer.ProcsGained.ShouldBe(0);
        analyzer.CelestialShotCasts.ShouldBe(1);
        analyzer.CelestialShotCastsWithImpetus.ShouldBe(0);
        analyzer.CelestialShotCastsWithoutImpetus.ShouldBe(1);
        analyzer.CelestialShotWithImpetusPercentage.ShouldBe(0d);
    }

    [Fact]
    public async Task ProcRemoved_SecondsFromAnyCelestialShot_IsExpired()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            CelestialShot(2_000),
            RemoveBuff(7_000));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task ProcRemoved_WithNoCelestialShotAtAll_IsExpired()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            RemoveBuff(16_000));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(1);
        analyzer.CelestialShotCasts.ShouldBe(0);
        analyzer.CelestialShotWithImpetusPercentage.ShouldBe(0d);
    }

    [Fact]
    public async Task TwoStacks_FallingOffInOneRemoval_CountAsOneExpiry()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            ApplyBuffStack(2_000, stack: 2),
            RemoveBuff(17_000));

        analyzer.ProcsGained.ShouldBe(2);
        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task TwoStacks_SpentByTwoCelestialShots_AreBothCountedAsConsumed()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            ApplyBuffStack(2_000, stack: 2),
            CelestialShot(3_000),
            RemoveBuffStack(3_010, stack: 1),
            CelestialShot(4_000),
            RemoveBuff(4_020));

        analyzer.ProcsGained.ShouldBe(2);
        analyzer.ProcsConsumed.ShouldBe(2);
        analyzer.ProcsExpired.ShouldBe(0);
        analyzer.CelestialShotCasts.ShouldBe(2);
        analyzer.CelestialShotCastsWithImpetus.ShouldBe(2);
    }

    [Fact]
    public async Task OneCelestialShot_CannotClaimTwoRemovals()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            ApplyBuffStack(1_500, stack: 2),
            CelestialShot(2_000),
            RemoveBuffStack(2_050, stack: 1),
            RemoveBuff(2_100));

        analyzer.ProcsGained.ShouldBe(2);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshBuff_KeepsTheProcBankedForTheNextCelestialShot()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            RefreshBuff(9_000),
            CelestialShot(10_000),
            RemoveBuff(10_030));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.CelestialShotCastsWithImpetus.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CelestialImpetus_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            ApplyBuff(1_000),
            CelestialShot(2_000),
            RemoveBuff(2_050));

        var entry = parser.CelestialImpetusAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.CelestialImpetusAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).CelestialImpetusAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent CelestialShot(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Activation = true,
        Ability = new SpellAbility { FSLID = Spells.CelestialShot.FSLID, Name = Spells.CelestialShot.Name },
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ImpetusAbility(),
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = ImpetusAbility(),
    };

    private static RefreshBuffEvent RefreshBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ImpetusAbility(),
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ImpetusAbility(),
    };

    private static RemoveBuffStackEvent RemoveBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = ImpetusAbility(),
    };

    private static SpellAbility ImpetusAbility() => new()
    {
        FSLID = Spells.CelestialImpetus.FSLID,
        Name = Spells.CelestialImpetus.Name,
    };

    private static async Task<CelestialImpetusAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(events);
        return parser.CelestialImpetusAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(ElarionCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
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
}
