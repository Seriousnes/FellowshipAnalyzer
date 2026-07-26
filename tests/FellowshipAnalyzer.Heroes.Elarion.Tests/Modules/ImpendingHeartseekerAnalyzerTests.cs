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

public sealed class ImpendingHeartseekerAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 20_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task WithoutTheTalent_NoAnalyzerIsConstructed()
    {
        var (parser, _) = await AnalyzeAsync(
            ApplyBuff(1_000),
            Barrage(2_000),
            RemoveBuff(2_050));

        parser.ImpendingHeartseekerAnalyzers.ShouldBeEmpty();
        parser.CelestialImpetusAnalyzers.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WithTheTalent_TheAnalyzerRunsOnThePull()
    {
        var (parser, _) = await AnalyzeAsync(
            Talented(),
            ApplyBuff(1_000));

        parser.ImpendingHeartseekerAnalyzers.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BuffRemoved_JustAfterABarrage_IsConsumed()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            Barrage(2_000),
            RemoveBuff(2_050));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);
        analyzer.PullDurationMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task BuffRemoved_SecondsFromAnyBarrage_IsExpired()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            Barrage(2_000),
            RemoveBuff(16_000));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task OneBarrage_CannotClaimTwoRemovals()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            ApplyBuffStack(1_500, stack: 2),
            Barrage(2_000),
            RemoveBuffStack(2_040, stack: 1),
            RemoveBuff(2_080));

        analyzer.ProcsGained.ShouldBe(2);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ImpendingHeartseeker_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            Talented(),
            ApplyBuff(1_000),
            Barrage(2_000),
            RemoveBuff(2_050));

        var entry = parser.ImpendingHeartseekerAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.ImpendingHeartseekerAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).ImpendingHeartseekerAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CombatantInfoEvent Talented() => new()
    {
        Timestamp = 0,
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = Talents.ImpendingHeartseeker.FSLID }],
    };

    private static CastEvent Barrage(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new SpellAbility
        {
            FSLID = Spells.HeartseekerBarrage.FSLID,
            Name = Spells.HeartseekerBarrage.Name,
        },
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = BuffAbility(),
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static RemoveBuffStackEvent RemoveBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = BuffAbility(),
    };

    private static SpellAbility BuffAbility() => new()
    {
        FSLID = Spells.ImpendingHeartseeker.FSLID,
        Name = Spells.ImpendingHeartseeker.Name,
    };

    private static async Task<ImpendingHeartseekerAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync([Talented(), .. events]);
        return parser.ImpendingHeartseekerAnalyzers.ShouldHaveSingleItem().Analyzer;
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
