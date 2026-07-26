using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class FocusEconomyAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;

    private const int LogResourceScale = 100;
    private const int MaxFocus = 100;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 20_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task FocusedShot_CountsOnlyTheCastsStartedNearCap()
    {
        var analyzer = await Analyze(
            Cast(1_000, Spells.FocusedShot.FSLID, focus: 95),
            Cast(2_000, Spells.FocusedShot.FSLID, focus: 50));

        analyzer.FocusedShotCasts.ShouldBe(2);
        analyzer.FocusedShotCastsNearCap.ShouldBe(1);
    }

    [Fact]
    public async Task Snapshots_CountTheCastsSittingAtFullFocus()
    {
        var analyzer = await Analyze(
            Cast(1_000, Spells.FocusedShot.FSLID, focus: MaxFocus),
            Cast(2_000, Spells.CelestialShot.FSLID, focus: MaxFocus),
            Cast(3_000, Spells.Multishot.FSLID, focus: 40),
            Cast(4_000, Spells.Shoot.FSLID, focus: null));

        analyzer.SampledEvents.ShouldBe(3);
        analyzer.SampledAtCap.ShouldBe(2);
    }

    [Fact]
    public async Task EventHorizon_CountsTheSpendersBetweenTheCastAndTheBuffRemoval()
    {
        var analyzer = await Analyze(
            Cast(1_000, Spells.EventHorizon.FSLID, focus: 80),
            ApplyBuff(1_100, Spells.EventHorizonBuff.FSLID),
            Cast(2_000, Spells.CelestialShot.FSLID, focus: 80),
            Cast(3_000, Spells.Multishot.FSLID, focus: 72),
            Cast(4_000, Spells.HighwindArrow.FSLID, focus: 62),
            RemoveBuff(5_000, Spells.EventHorizonBuff.FSLID),
            Cast(6_000, Spells.StarfallVolley.FSLID, focus: 47));

        var window = analyzer.EventHorizonCasts.ShouldHaveSingleItem();
        window.Timestamp.ShouldBe(1_000);
        window.FocusAtCast.ShouldBe(80);
        window.SpendersInWindow.ShouldBe(3);
        window.FocusSpentInWindow.ShouldBe((15 / 2) + (20 / 2) + (30 / 2));
    }

    [Fact]
    public async Task EventHorizon_WithoutABuffRemovalRunsToTheEndOfThePull()
    {
        var analyzer = await Analyze(
            Cast(1_000, Spells.EventHorizon.FSLID, focus: 100),
            ApplyBuff(1_100, Spells.EventHorizonBuff.FSLID),
            Cast(2_000, Spells.HeartseekerBarrage.FSLID, focus: 100),
            Cast(19_000, Spells.LunarlightMark.FSLID, focus: 85));

        var window = analyzer.EventHorizonCasts.ShouldHaveSingleItem();
        window.FocusAtCast.ShouldBe(100);
        window.SpendersInWindow.ShouldBe(2);
        window.FocusSpentInWindow.ShouldBe((30 / 2) + (20 / 2));
        analyzer.PullDurationMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task Analyze_FocusEconomy_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            Cast(1_000, Spells.EventHorizon.FSLID, focus: 90),
            RemoveBuff(2_000, Spells.EventHorizonBuff.FSLID));

        var entry = parser.FocusEconomyAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.FocusEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).FocusEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent Cast(int timestamp, int abilityId, int? focus) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new SpellAbility { FSLID = abilityId, Name = $"Spell {abilityId}" },
        SourceResources = focus is null
            ? null
            : new ActorResources
            {
                Resources =
                [
                    new ClassResource
                    {
                        Type = ResourceTypes.Primary,
                        Amount = focus.Value * LogResourceScale,
                        Max = MaxFocus * LogResourceScale,
                    },
                ],
            },
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new SpellAbility { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new SpellAbility { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static async Task<FocusEconomyAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(events);
        return parser.FocusEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
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
