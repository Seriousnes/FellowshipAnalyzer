using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Utility;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Modules;

public sealed class WintersEmbraceUpliftTrackerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;

    private const int WindowStart = 1_000;
    private const int WindowEnd = 4_000;

    private static readonly ReportDungeon Dungeon = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 60_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task DamageInsideAWindow_AccruesTheMarginalShareOfTheTwentyPercentAmplifier()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 1_200),
            Damage(WindowStart + 200, Spells.FrostBolt, 600),
            EmbraceRemoved(WindowEnd));

        tracker.TotalBonusDamage.ShouldBe(300);
        tracker.AmplifiedEventCount.ShouldBe(2);
    }

    [Fact]
    public async Task AccrualMatchesCombatMathOnTheSameEvents()
    {
        var hits = new[] { 1_337L, 42L, 999_999L };
        var expected = hits.Sum(amount =>
            CombatMath.CalculateEffectiveDamage(
                Damage(WindowStart, Spells.FrostBolt, amount),
                WintersEmbraceUpliftTracker.DamageIncrease));

        var events = new List<Event> { EmbraceApplied(WindowStart) };
        events.AddRange(hits.Select((amount, index) => (Event)Damage(WindowStart + index + 1, Spells.FrostBolt, amount)));
        events.Add(EmbraceRemoved(WindowEnd));

        var tracker = await Analyze([.. events]);

        tracker.TotalBonusDamage.ShouldBe(expected);
    }

    [Fact]
    public async Task AbsorbedDamageCountsTowardsTheAmplifiedHit()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 700, absorbed: 500),
            EmbraceRemoved(WindowEnd));

        tracker.TotalBonusDamage.ShouldBe(200);
    }

    [Fact]
    public async Task BurstingIceAndItsDamageEffect_AreNotAmplified()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.BurstingIce, 1_200),
            Damage(WindowStart + 200, Spells.BurstingIceDamage, 1_200),
            Damage(WindowStart + 300, Spells.FrostBolt, 1_200),
            EmbraceRemoved(WindowEnd));

        tracker.TotalBonusDamage.ShouldBe(200);
        tracker.AmplifiedEventCount.ShouldBe(1);
        tracker.BySpell.ShouldHaveSingleItem().Name.ShouldBe(Spells.FrostBolt.Name);
    }

    [Fact]
    public async Task DamageOutsideAWindow_IsNotAmplifiedButStillCountsTowardsDungeonDamage()
    {
        var tracker = await Analyze(
            Damage(500, Spells.FrostBolt, 1_200),
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 1_200),
            EmbraceRemoved(WindowEnd),
            Damage(WindowEnd + 500, Spells.FrostBolt, 1_200));

        tracker.TotalBonusDamage.ShouldBe(200);
        tracker.AmplifiedEventCount.ShouldBe(1);
        tracker.TotalDamage.ShouldBe(3_600);
    }

    [Fact]
    public async Task UpliftShare_MeasuresTheBonusAgainstAllPlayerDamage()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 1_200),
            EmbraceRemoved(WindowEnd),
            Damage(WindowEnd + 500, Spells.FrostBolt, 800));

        tracker.TotalBonusDamage.ShouldBe(200);
        tracker.TotalDamage.ShouldBe(2_000);
        tracker.UpliftShare.ShouldBe(0.1, 1e-9);
    }

    [Fact]
    public async Task PerSpellAttribution_SplitsBySpellAndOrdersTheHeaviestFirst()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 600),
            Damage(WindowStart + 200, Spells.GlacialBlast, 1_200),
            Damage(WindowStart + 300, Spells.GlacialBlast, 1_200),
            EmbraceRemoved(WindowEnd));

        tracker.BySpell.Count.ShouldBe(2);
        tracker.BySpell[0].AbilityId.ShouldBe(Spells.GlacialBlast.FSLID.Value);
        tracker.BySpell[0].Name.ShouldBe(Spells.GlacialBlast.Name);
        tracker.BySpell[0].BonusDamage.ShouldBe(400);
        tracker.BySpell[1].Name.ShouldBe(Spells.FrostBolt.Name);
        tracker.BySpell[1].BonusDamage.ShouldBe(100);
        tracker.BySpell.Sum(uplift => uplift.BonusDamage).ShouldBe(tracker.TotalBonusDamage);
    }

    [Fact]
    public async Task ReapplyWhileTheBuffIsActive_LeavesAccrualRunning()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 1_200),
            EmbraceApplied(WindowStart + 200),
            Damage(WindowStart + 300, Spells.FrostBolt, 1_200),
            EmbraceRemoved(WindowEnd));

        tracker.AmplifiedEventCount.ShouldBe(2);
        tracker.TotalBonusDamage.ShouldBe(400);
    }

    [Fact]
    public async Task RemovalWithNoBuffActive_IsIgnored()
    {
        var tracker = await Analyze(
            EmbraceRemoved(500),
            Damage(600, Spells.FrostBolt, 1_200),
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 1_200),
            EmbraceRemoved(WindowEnd));

        tracker.AmplifiedEventCount.ShouldBe(1);
        tracker.TotalBonusDamage.ShouldBe(200);
    }

    [Fact]
    public async Task NothingAmplified_ContributesNoStatisticsCard()
    {
        var tracker = await Analyze(Damage(500, Spells.FrostBolt, 1_200));

        tracker.AmplifiedEventCount.ShouldBe(0);
        tracker.TotalBonusDamage.ShouldBe(0);
        tracker.UpliftShare.ShouldBe(0);
        tracker.Statistic.ShouldBeNull();
    }

    [Fact]
    public async Task AmplifiedDamage_ContributesTheStatisticsCard()
    {
        var tracker = await Analyze(
            EmbraceApplied(WindowStart),
            Damage(WindowStart + 100, Spells.FrostBolt, 1_200),
            EmbraceRemoved(WindowEnd));

        tracker.Statistic.ShouldNotBeNull();
    }

    private static ApplyBuffEvent EmbraceApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Spells.WintersEmbrace.FSLID, Name = Spells.WintersEmbrace.Name },
    };

    private static RemoveBuffEvent EmbraceRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Spells.WintersEmbrace.FSLID, Name = Spells.WintersEmbrace.Name },
    };

    private static DamageEvent Damage(int timestamp, Spell spell, long amount, long? absorbed = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Amount = amount,
        Absorbed = absorbed,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static async Task<WintersEmbraceUpliftTracker> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Dungeon);
        return parser.GetModule<WintersEmbraceUpliftTracker>().ShouldNotBeNull();
    }
}
