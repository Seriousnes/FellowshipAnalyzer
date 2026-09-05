using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

/// <summary>
/// Tests for the gain view <see cref="ChronaTracker.GainsBetween"/> serves: the rise the snapshots
/// recorded, the amount the game data states, and the overcap only the two together can reveal.
/// </summary>
public sealed class ChronaTrackerGainsTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;
    private const int RawChronaCap = 10_000;

    [Fact]
    public async Task AGainUnderTheCap_ReportsTheRiseTheSnapshotsRecorded()
    {
        var tracker = await Analyze(
            Damage(1_000, Spells.TimeShard.FSLID, rawChrona: 2_000),
            Damage(2_000, Spells.TimeShard.FSLID, rawChrona: 2_600));

        var gain = tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();

        gain.Before.ShouldBe(20);
        gain.Usable.ShouldBe(6);
        gain.Overcap.ShouldBe(0);
        gain.AbilityId.ShouldBe(Spells.TimeShard.FSLID.Value);
    }

    [Fact]
    public async Task AGainClippedByTheCap_ReportsTheStatedAmountAsOvercap()
    {
        var tracker = await Analyze(
            Damage(1_000, Spells.TimeShard.FSLID, rawChrona: 9_800),
            Damage(2_000, Spells.TimeShard.FSLID, rawChrona: RawChronaCap));

        var gain = tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();

        gain.Usable.ShouldBe(2);
        gain.Gain.ShouldBe(6);
        gain.Overcap.ShouldBe(4);
        tracker.OvercapBetween(ResourceTypes.Primary, 0, 10_000).ShouldBe(4);
    }

    [Fact]
    public async Task AnAbilityTheRegistryStatesNoGenerationFor_ReportsNoOvercap()
    {
        var tracker = await Analyze(
            Damage(1_000, Spells.Oblivion.FSLID, rawChrona: 9_800),
            Damage(2_000, Spells.Oblivion.FSLID, rawChrona: RawChronaCap));

        var gain = tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();

        gain.Usable.ShouldBe(2);
        gain.Overcap.ShouldBe(0);
    }

    [Fact]
    public async Task ATickArrivingUnderTheEffectId_IsAttributedToItsAbility()
    {
        var tracker = await Analyze(
            Damage(1_000, Spells.EntropyClaimDot.FSLID, rawChrona: 2_000),
            Damage(2_000, Spells.EntropyClaimDot.FSLID, rawChrona: 2_400));

        var gain = tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();

        gain.AbilityId.ShouldBe(Spells.EntropyClaim.FSLID.Value);
        tracker.GeneratedByAbilityBetween(ResourceTypes.Primary, Spells.EntropyClaim.FSLID, 0, 10_000).ShouldBe(4);
    }

    [Fact]
    public async Task WithSynchronicityBelowHalf_TheRaisedShareIsReported()
    {
        var tracker = await Analyze(
            Talented(AeonaTalents.Synchronicity),
            Damage(1_000, Spells.TimeShard.FSLID, rawChrona: 1_000),
            Damage(2_000, Spells.TimeShard.FSLID, rawChrona: 1_750));

        var gain = tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();

        gain.Before.ShouldBe(10);
        gain.Usable.ShouldBe(7);
        gain.SynchronicityChrona.ShouldBe(1);
    }

    [Fact]
    public async Task WithSynchronicityAboveHalf_NoShareIsReported()
    {
        var tracker = await Analyze(
            Talented(AeonaTalents.Synchronicity),
            Damage(1_000, Spells.TimeShard.FSLID, rawChrona: 6_000),
            Damage(2_000, Spells.TimeShard.FSLID, rawChrona: 6_600));

        var gain = tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();

        gain.Before.ShouldBe(60);
        gain.SynchronicityChrona.ShouldBe(0);
    }

    [Fact]
    public async Task WithoutSynchronicity_NoShareIsReported()
    {
        var tracker = await Analyze(
            Damage(1_000, Spells.TimeShard.FSLID, rawChrona: 1_000),
            Damage(2_000, Spells.TimeShard.FSLID, rawChrona: 1_600));

        tracker.GainsBetween(ResourceTypes.Primary, 0, 10_000)
            .ShouldHaveSingleItem()
            .SynchronicityChrona.ShouldBe(0);
    }

    [Fact]
    public async Task SnapshotAt_IsWithheldBeforeTheFirstReading()
    {
        var tracker = await Analyze(Damage(5_000, Spells.TimeShard.FSLID, rawChrona: 2_000));

        tracker.SnapshotAt(ResourceTypes.Primary, 1_000).ShouldBeNull();
        tracker.SnapshotAt(ResourceTypes.Primary, 5_000).ShouldBe(20);
        tracker.AmountAt(ResourceTypes.Primary, 1_000).ShouldBe(0);
    }

    [Fact]
    public async Task OvercapByAbility_CountsOnlyThatAbility()
    {
        var tracker = await Analyze(
            Damage(1_000, Spells.TimeShard.FSLID, rawChrona: 9_800),
            Damage(2_000, Spells.TimeShard.FSLID, rawChrona: RawChronaCap),
            Damage(3_000, Spells.UnfoldingDoom.FSLID, rawChrona: RawChronaCap));

        tracker.OvercapByAbilityBetween(ResourceTypes.Primary, Spells.TimeShard.FSLID, 0, 10_000).ShouldBe(4);
        tracker.OvercapByAbilityBetween(ResourceTypes.Primary, Spells.UnfoldingDoom.FSLID, 0, 10_000).ShouldBe(0);
    }

    private static DamageEvent Damage(int timestamp, int abilityId, int rawChrona) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Amount = 500,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        SourceResources = new ActorResources
        {
            HitPoints = 20_000,
            MaxHitPoints = 40_000,
            Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = rawChrona, Max = RawChronaCap }],
        },
    };

    private static CombatantInfoEvent Talented(params int[] talentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talentIds.Select(id => new TalentInfo { Id = id })],
    };

    private static async Task<ChronaTracker> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "Boss", 1, true, 0, 120_000, null, null, null));

        return parser.ChronaTracker.ShouldNotBeNull();
    }
}
