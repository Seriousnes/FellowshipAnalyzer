using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class SalvoTrackerTests
{
    private const int PlayerId = 1;
    private const int TargetA = 20;
    private const int TargetB = 21;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 20_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task SalvoAndErupt_AreTotalledAsAShareOfThePlayersDamage()
    {
        var parser = await AnalyzeAsync(
            Damage(1_000, TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 900),
            Damage(1_100, TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 600),
            Damage(1_200, TargetB, Spells.LunarlightMarkAoeDamage.FSLID, amount: 500),
            Damage(1_300, TargetA, Spells.CelestialShotDamage.FSLID, amount: 8_000));

        var tracker = parser.SalvoTracker.ShouldNotBeNull();
        tracker.SalvoHits.ShouldBe(2);
        tracker.EruptHits.ShouldBe(1);
        tracker.TotalHits.ShouldBe(3);
        tracker.SalvoDamage.ShouldBe(1_500);
        tracker.EruptDamage.ShouldBe(500);
        tracker.TotalMarkDamage.ShouldBe(2_000);
        tracker.TotalPlayerDamage.ShouldBe(10_000);
        tracker.DamageShare.ShouldBe(0.2d);
    }

    [Fact]
    public async Task SalvoHits_MakeTheStatisticsCardAvailable()
    {
        var parser = await AnalyzeAsync(
            Damage(1_000, TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 900));

        var tracker = parser.SalvoTracker.ShouldNotBeNull();
        tracker.Statistic.ShouldNotBeNull();
    }

    [Fact]
    public async Task WithoutSalvoHits_TheStatisticsCardIsWithheld()
    {
        var parser = await AnalyzeAsync(
            Damage(1_000, TargetA, Spells.CelestialShotDamage.FSLID, amount: 8_000));

        var tracker = parser.SalvoTracker.ShouldNotBeNull();
        tracker.SalvoHits.ShouldBe(0);
        tracker.EruptHits.ShouldBe(0);
        tracker.TotalPlayerDamage.ShouldBe(8_000);
        tracker.DamageShare.ShouldBe(0d);
        tracker.Statistic.ShouldBeNull();
    }

    private static DamageEvent Damage(int timestamp, int targetId, int abilityId, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = 1,
        Amount = amount,
        Ability = new SpellAbility { FSLID = abilityId, Name = $"Spell {abilityId}" },
    };

    private static async Task<ElarionCombatLogParser> AnalyzeAsync(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Fight);
        return parser;
    }
}
