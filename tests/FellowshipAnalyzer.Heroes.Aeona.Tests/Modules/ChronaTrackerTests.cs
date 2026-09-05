using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaSpells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

/// <summary>
/// Exercises the Chrona and mana reconstruction against fabricated snapshots. Aeona logs carry no
/// <see cref="ResourceChangeEvent"/> and no <see cref="ClassResource.Cost"/>, so the tracker's own
/// snapshot ledger is the only path real data reaches, and the resource-change branches can only be
/// reached from fabricated events.
/// Snapshot amounts are written at the raw log scale because <c>ResourceNormalizer</c> divides them by
/// 100 before dispatch; <see cref="ResourceChangeEvent"/> amounts are written in game units because it
/// leaves those untouched.
/// </summary>
public sealed class ChronaTrackerTests
{
    private const int PlayerId = 7;
    private const int TankId = 9;
    private const int EnemyId = 100;
    private const int RawChronaCap = 10_000;

    [Fact]
    public async Task FirstSnapshot_SeedsTheLedgerWithoutCountingGeneration()
    {
        var tracker = await Analyze(Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 7_000));

        tracker.TotalGenerated(ResourceTypes.Primary).ShouldBe(0);
        tracker.TotalSpent(ResourceTypes.Primary).ShouldBe(0);
        tracker.TotalWasted(ResourceTypes.Primary).ShouldBe(0);
        tracker.AmountAt(ResourceTypes.Primary, 1_000).ShouldBe(70);
        tracker.EventsBetween(ResourceTypes.Primary, 0, 10_000).ShouldBeEmpty();
    }

    [Fact]
    public async Task RisingSnapshots_CountAsGenerationAttributedToTheCarryingAbility()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000),
            Heal(2_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_600),
            Heal(3_000, AeonaSpells.TemporalBarrage.FSLID, rawChrona: 5_000));

        tracker.TotalGenerated(ResourceTypes.Primary).ShouldBe(10);
        tracker.TotalWasted(ResourceTypes.Primary).ShouldBe(0);
        tracker.GeneratedBetween(ResourceTypes.Primary, 0, 10_000).ShouldBe(10);

        tracker.GeneratedByAbilityBetween(ResourceTypes.Primary, 0, 10_000).ShouldBe(
        [
            new AbilityResourceGain(AeonaSpells.EntropyClaim.FSLID, 6, 0),
            new AbilityResourceGain(AeonaSpells.TemporalBarrage.FSLID, 4, 0),
        ]);
    }

    [Fact]
    public async Task FallingSnapshots_CountAsSpendingAttributedToTheMostRecentCast()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 7_000),
            Cast(2_000, AeonaSpells.Oblivion.FSLID, rawChrona: 7_000),
            Heal(3_000, AeonaSpells.Oblivion.FSLID, rawChrona: 4_000));

        tracker.TotalSpent(ResourceTypes.Primary).ShouldBe(30);
        tracker.SpentBetween(ResourceTypes.Primary, 0, 10_000).ShouldBe(30);
        tracker.AmountAt(ResourceTypes.Primary, 3_000).ShouldBe(40);

        var spend = tracker.EventsBetween(ResourceTypes.Primary, 0, 10_000).ShouldHaveSingleItem();
        spend.Kind.ShouldBe(ResourceEventKind.Spend);
        spend.Id.ShouldBe(AeonaSpells.Oblivion.FSLID.Value);
        spend.Amount.ShouldBe(30);
    }

    [Fact]
    public async Task GainOnACastSnapshot_IsLeftUnattributed()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000),
            Cast(2_000, AeonaSpells.Oblivion.FSLID, rawChrona: 5_000));

        tracker.GeneratedByAbilityBetween(ResourceTypes.Primary, 0, 10_000)
            .ShouldBe([new AbilityResourceGain(0, 10, 0)]);
    }

    [Fact]
    public async Task ResourceChangeCrossingTheCap_RecordsTheExcessAsWaste()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 9_000),
            ResourceChange(2_000, AeonaSpells.EntropyClaim.FSLID, ResourceTypes.Primary, change: 20, waste: 0),
            Heal(3_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 10_000));

        tracker.TotalGenerated(ResourceTypes.Primary).ShouldBe(10);
        tracker.TotalWasted(ResourceTypes.Primary).ShouldBe(10);
        tracker.WastedBetween(ResourceTypes.Primary, 0, 10_000).ShouldBe(10);
        tracker.AmountAt(ResourceTypes.Primary, 2_000).ShouldBe(tracker.MaxOf(ResourceTypes.Primary));
        tracker.AmountAt(ResourceTypes.Primary, 3_000).ShouldBe(100);
        tracker.EventsBetween(ResourceTypes.Primary, 2_500, 10_000).ShouldBeEmpty();
    }

    [Fact]
    public async Task ResourceChangeBeforeAnySnapshot_IsCountedAndTheSnapshotStillSeedsTheAmount()
    {
        var tracker = await Analyze(
            ResourceChange(1_000, AeonaSpells.EntropyClaim.FSLID, ResourceTypes.Primary, change: 12, waste: 0),
            Heal(2_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 7_000),
            Heal(3_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 7_500));

        tracker.TotalGenerated(ResourceTypes.Primary).ShouldBe(17);
        tracker.TotalWasted(ResourceTypes.Primary).ShouldBe(0);
        tracker.AmountAt(ResourceTypes.Primary, 1_000).ShouldBe(12);
        tracker.AmountAt(ResourceTypes.Primary, 2_000).ShouldBe(70);
        tracker.AmountAt(ResourceTypes.Primary, 3_000).ShouldBe(75);
    }

    [Fact]
    public async Task ResourceChangeBelowTheCap_KeepsTheDeclaredWaste()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 5_000),
            ResourceChange(2_000, AeonaSpells.EntropyClaim.FSLID, ResourceTypes.Primary, change: 20, waste: 5));

        tracker.TotalGenerated(ResourceTypes.Primary).ShouldBe(15);
        tracker.TotalWasted(ResourceTypes.Primary).ShouldBe(5);
        tracker.AmountAt(ResourceTypes.Primary, 2_000).ShouldBe(65);
    }

    [Fact]
    public async Task WindowedAccessors_CountOnlyChangesInsideTheWindow()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000),
            Heal(2_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 5_000),
            Cast(3_000, AeonaSpells.Oblivion.FSLID, rawChrona: 5_000),
            Heal(4_000, AeonaSpells.Oblivion.FSLID, rawChrona: 2_000),
            Heal(5_000, AeonaSpells.TemporalBarrage.FSLID, rawChrona: 3_000));

        tracker.GeneratedBetween(ResourceTypes.Primary, 2_000, 4_000).ShouldBe(10);
        tracker.SpentBetween(ResourceTypes.Primary, 2_000, 4_000).ShouldBe(30);
        tracker.GeneratedBetween(ResourceTypes.Primary, 5_000, 5_000).ShouldBe(10);
        tracker.SpentBetween(ResourceTypes.Primary, 5_000, 5_000).ShouldBe(0);
        tracker.EventsBetween(ResourceTypes.Primary, 4_500, 6_000).ShouldHaveSingleItem().Timestamp.ShouldBe(5_000);
        tracker.EventsBetween(ResourceTypes.Primary, 6_000, 9_000).ShouldBeEmpty();

        tracker.GeneratedByAbilityBetween(ResourceTypes.Primary, 4_500, 6_000)
            .ShouldBe([new AbilityResourceGain(AeonaSpells.TemporalBarrage.FSLID, 10, 0)]);
    }

    [Fact]
    public async Task AmountAt_ReturnsTheLatestObservationAtOrBeforeTheTimestamp()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000),
            Heal(3_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 6_000));

        tracker.AmountAt(ResourceTypes.Primary, 500).ShouldBe(0);
        tracker.AmountAt(ResourceTypes.Primary, 1_000).ShouldBe(40);
        tracker.AmountAt(ResourceTypes.Primary, 2_999).ShouldBe(40);
        tracker.AmountAt(ResourceTypes.Primary, 3_000).ShouldBe(60);
        tracker.AmountAt(ResourceTypes.Primary, 99_000).ShouldBe(60);
    }

    [Fact]
    public async Task MaxOf_PrefersTheReportedMaximumAndFallsBackToTheChronaCap()
    {
        var empty = await Analyze();

        empty.MaxOf(ResourceTypes.Primary).ShouldBe(100);
        empty.MaxOf(ResourceTypes.Mana).ShouldBe(0);

        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000, rawChronaMax: 12_000));

        tracker.MaxOf(ResourceTypes.Primary).ShouldBe(120);
    }

    [Fact]
    public async Task ManaIsTrackedAlongsideChronaAndOtherResourcesAreIgnored()
    {
        var tracker = await Analyze(
            Heal(1_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000, rawMana: 100_000),
            Heal(2_000, AeonaSpells.EntropyClaim.FSLID, rawChrona: 4_000, rawMana: 120_000));

        tracker.TotalGenerated(ResourceTypes.Mana).ShouldBe(200);
        tracker.MaxOf(ResourceTypes.Mana).ShouldBe(1_656);
        tracker.AmountAt(ResourceTypes.Mana, 2_000).ShouldBe(1_200);

        tracker.EventsBetween(ResourceTypes.Stagger, 0, 10_000).ShouldBeEmpty();
        tracker.EventsBetween(ResourceTypes.Spirit, 0, 10_000).ShouldBeEmpty();
        tracker.AmountAt(ResourceTypes.Stagger, 2_000).ShouldBe(0);
        tracker.AmountAt(ResourceTypes.Spirit, 2_000).ShouldBe(0);
        tracker.TotalGenerated(ResourceTypes.Stagger).ShouldBe(0);
    }

    [Fact]
    public async Task Tracker_ResolvesFromTheParserAndLabelsChrona()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        var result = await parser.Analyze([], PlayerId, BossDungeon());

        var tracker = parser.ChronaTracker.ShouldNotBeNull();
        result.Modules.OfType<ChronaTracker>().ShouldHaveSingleItem().ShouldBeSameAs(tracker);
        tracker.GetDisplayName(ResourceTypes.Primary).ShouldBe("Chrona");
        tracker.GetDisplayName(ResourceTypes.Mana).ShouldBe("Mana");
    }

    private static async Task<ChronaTracker> Analyze(params Event[] events)
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, BossDungeon());

        return parser.ChronaTracker.ShouldNotBeNull();
    }

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        return services;
    }

    private static ReportDungeon BossDungeon() =>
        new(0, "Boss", 1, true, 0, 120_000, null, null, null);

    private static HealEvent Heal(
        int timestamp,
        int abilityId,
        int rawChrona,
        int rawChronaMax = RawChronaCap,
        int? rawMana = null) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = TankId,
            Amount = 5_000,
            Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
            SourceResources = PlayerResources(rawChrona, rawChronaMax, rawMana),
        };

    private static CastEvent Cast(
        int timestamp,
        int abilityId,
        int rawChrona,
        int rawChronaMax = RawChronaCap) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = EnemyId,
            Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
            Target = new StubCastTarget(),
            Channel = new EndChannelEvent(),
            SourceResources = PlayerResources(rawChrona, rawChronaMax, rawMana: null),
        };

    private static ResourceChangeEvent ResourceChange(
        int timestamp,
        int abilityId,
        ResourceTypes type,
        double change,
        double waste) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = PlayerId,
            Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
            ResourceChangeType = type,
            ResourceChange = change,
            Waste = waste,
        };

    private static ActorResources PlayerResources(int rawChrona, int rawChronaMax, int? rawMana)
    {
        var resources = new List<ClassResource>
        {
            new() { Type = ResourceTypes.Primary, Amount = rawChrona, Max = rawChronaMax },
            new() { Type = ResourceTypes.Spirit, Amount = 3_000, Max = 13_000 },
            new() { Type = ResourceTypes.Stagger, Amount = 21_300, Max = -100 },
        };

        if (rawMana is not null)
            resources.Insert(0, new ClassResource { Type = ResourceTypes.Mana, Amount = rawMana.Value, Max = 165_600 });

        return new ActorResources { HitPoints = 20_000, MaxHitPoints = 30_000, Resources = resources };
    }

    private sealed class StubCastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
