using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="Enemies"/>: the roster each pull projects out of the seeded population,
/// instance resolution at the roster boundary, the population over time, and the enemy-facing aura
/// queries.
/// The fixtures below are shaped after report <c>6fgrXtW1b2aTZcD3</c> dungeon 36.
/// </summary>
public sealed partial class EnemiesTests
{
    private const int PlayerId = 25;
    private const int Ally = 30;
    private const int TrashCaster = 19;
    private const int TrashMelee = 21;
    private const int Boss = 24;
    private const int BloodCrystal = 28;
    private const int BossPet = 40;
    private const int Environment = -1;

    private const int FirstPullStart = 1_000;
    private const int FirstPullEnd = 3_000;
    private const int BossPullStart = 5_000;
    private const int BossPullEnd = 8_000;

    private static readonly ReportDungeon Dungeon = new(
        Id: 0, Name: "Scryer's Peak", EncounterId: 0, Kill: null,
        StartTime: 0, EndTime: 10_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
        DungeonPulls:
        [
            new DungeonPull(Id: 1, EncounterId: 0, Kill: null,
                StartTime: FirstPullStart, EndTime: FirstPullEnd, Name: "Trash",
                EnemyNpcs:
                [
                    new DungeonPullNpc(TrashCaster, GameId: 190, MinimumInstanceId: 1, MaximumInstanceId: 1, null, null),
                    new DungeonPullNpc(TrashMelee, GameId: 210, MinimumInstanceId: 1, MaximumInstanceId: 2, null, null),
                ]),
            new DungeonPull(Id: 2, EncounterId: 42, Kill: true,
                StartTime: BossPullStart, EndTime: BossPullEnd, Name: "Overlord Varux",
                EnemyNpcs:
                [
                    new DungeonPullNpc(TrashCaster, GameId: 190, MinimumInstanceId: 2, MaximumInstanceId: 2, null, null),
                    new DungeonPullNpc(Boss, GameId: 240, MinimumInstanceId: null, MaximumInstanceId: null, null, null),
                    new DungeonPullNpc(BossPet, GameId: 400, MinimumInstanceId: 1, MaximumInstanceId: 3, null, null),
                ]),
        ],
        EnemyNpcs:
        [
            new DungeonNpc(TrashCaster, GameId: 190, InstanceCount: 2, GroupCount: 1, PetOwner: null),
            new DungeonNpc(TrashMelee, GameId: 210, InstanceCount: 2, GroupCount: 1, PetOwner: null),
            new DungeonNpc(Boss, GameId: 240, InstanceCount: 1, GroupCount: 1, PetOwner: null),
            new DungeonNpc(BossPet, GameId: 400, InstanceCount: 3, GroupCount: 1, PetOwner: Boss),
        ]);

    private const int Boundary = 2_000;

    private static readonly ReportDungeon TouchingPullDungeon = new(
        Id: 0, Name: "Touching", EncounterId: 0, Kill: null,
        StartTime: 0, EndTime: 10_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
        DungeonPulls:
        [
            new DungeonPull(Id: 1, EncounterId: 0, Kill: null,
                StartTime: 1_000, EndTime: Boundary, Name: "First",
                EnemyNpcs: [new DungeonPullNpc(TrashCaster, GameId: 190, MinimumInstanceId: 1, MaximumInstanceId: 1, null, null)]),
            new DungeonPull(Id: 2, EncounterId: 0, Kill: null,
                StartTime: Boundary, EndTime: 5_000, Name: "Second",
                EnemyNpcs: [new DungeonPullNpc(TrashMelee, GameId: 210, MinimumInstanceId: 1, MaximumInstanceId: 3, null, null)]),
        ],
        EnemyNpcs:
        [
            new DungeonNpc(TrashCaster, GameId: 190, InstanceCount: 1, GroupCount: 1, PetOwner: null),
            new DungeonNpc(TrashMelee, GameId: 210, InstanceCount: 3, GroupCount: 1, PetOwner: null),
        ]);

    private static readonly List<ReportActor> TouchingPullActors =
    [
        new(PlayerId, "Selected", "Player", null, null, null),
        new(TrashCaster, "Sanguine Sybling", "NPC", null, null, null),
        new(TrashMelee, "Heskyr Magus", "NPC", null, null, null),
    ];

    private const int Bloodfang = 66;

    private static readonly ReportDungeon OverlappingRosterDungeon = new(
        Id: 0, Name: "Urrak Markets", EncounterId: 0, Kill: null,
        StartTime: 0, EndTime: 10_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
        DungeonPulls:
        [
            new DungeonPull(Id: 1, EncounterId: 0, Kill: null,
                StartTime: 1_000, EndTime: 2_000, Name: "First",
                EnemyNpcs: [new DungeonPullNpc(Bloodfang, GameId: 660, MinimumInstanceId: 1, MaximumInstanceId: 2, null, null)]),
            new DungeonPull(Id: 2, EncounterId: 0, Kill: null,
                StartTime: 3_000, EndTime: 6_000, Name: "Second",
                EnemyNpcs: [new DungeonPullNpc(Bloodfang, GameId: 660, MinimumInstanceId: 1, MaximumInstanceId: 4, null, null)]),
        ],
        EnemyNpcs: [new DungeonNpc(Bloodfang, GameId: 660, InstanceCount: 4, GroupCount: 1, PetOwner: null)]);

    private static readonly List<ReportActor> OverlappingRosterActors =
    [
        new(PlayerId, "Selected", "Player", null, null, null),
        new(Bloodfang, "Bloodfang", "NPC", null, null, null),
    ];

    private static readonly ReportDungeon SinglePullDungeon = new(
        Id: 0, Name: "Boss", EncounterId: 42, Kill: true,
        StartTime: 0, EndTime: 10_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
        DungeonPulls: null,
        EnemyNpcs:
        [
            new DungeonNpc(100, GameId: 1000, InstanceCount: 1, GroupCount: 1, PetOwner: null),
            new DungeonNpc(101, GameId: 1010, InstanceCount: 1, GroupCount: 1, PetOwner: null),
        ]);

    private static readonly List<ReportActor> DungeonActors =
    [
        new(PlayerId, "Selected", "Player", null, null, null),
        new(Ally, "Ally", "Player", null, null, null),
        new(TrashCaster, "Sanguine Sybling", "NPC", null, null, null),
        new(TrashMelee, "Heskyr Magus", "NPC", null, null, null),
        new(Boss, "Overlord Varux", "NPC", null, null, null),
        new(BloodCrystal, "Blood Crystal", "NPC", null, null, null),
        new(BossPet, "Varux's Thrall", "NPC", null, null, null),
        new(Environment, "Environment", "NPC", null, null, null),
    ];

    private static readonly List<ReportActor> AuraActors =
    [
        new(PlayerId, "Selected", "Player", null, null, null),
        new(Ally, "Ally", "Player", null, null, null),
        new(100, "First", "NPC", null, null, null),
        new(101, "Second", "NPC", null, null, null),
    ];

    private static List<Event> DungeonEvents() =>
    [
        Damage(1_050, TrashMelee, 1),
        Damage(1_100, BloodCrystal, 1),
        Damage(1_100, BloodCrystal, 2),
        Damage(1_300, Environment, null),
        new DamageEvent { Timestamp = 1_350, SourceId = Environment, TargetId = PlayerId, Ability = Hit },
        Death(1_500, TrashCaster, 1),
        Death(2_000, TrashMelee, 1),
        Death(2_500, PlayerId, null),
        Death(2_800, BloodCrystal, 1),
        Death(2_800, BloodCrystal, 2),
        Death(6_000, TrashCaster, 2),
        Death(BossPullEnd, Boss, null),
    ];

    [Fact]
    public async Task Roster_NamesTheUnitsEachPullsOwnSpansAdmit()
    {
        var (parser, enemies) = await RunDungeon();

        parser.Pulls.Count.ShouldBe(2);

        enemies.Roster(parser.Pulls[0]).Select(unit => unit.Key).ShouldBe(
            [new UnitKey(TrashCaster, 1), new UnitKey(TrashMelee, 1), new UnitKey(TrashMelee, 2)],
            ignoreOrder: true);
        enemies.Roster(parser.Pulls[1]).Select(unit => unit.Key).ShouldBe(
            [new UnitKey(TrashCaster, 2), new UnitKey(Boss, 1)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Roster_OnADungeonWithNoPulls_IsEveryDungeonNpcSpawnExceptPets()
    {
        var dungeon = SinglePullDungeon with
        {
            EnemyNpcs =
            [
                new DungeonNpc(100, GameId: 1000, InstanceCount: 2, GroupCount: 1, PetOwner: null),
                new DungeonNpc(101, GameId: 1010, InstanceCount: 3, GroupCount: 1, PetOwner: 100),
            ],
        };

        var (parser, enemies) = await Run(dungeon, [], AuraActors);

        enemies.Roster(parser.Pulls.ShouldHaveSingleItem()).Select(unit => unit.Key).ShouldBe(
            [new UnitKey(100, 1), new UnitKey(100, 2)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Roster_OnADungeonWithNoPulls_OmitsAnEnemyOnlyTheEventsName()
    {
        var dungeon = SinglePullDungeon with
        {
            EnemyNpcs = [new DungeonNpc(100, GameId: 1000, InstanceCount: 1, GroupCount: 1, PetOwner: null)],
        };

        var (parser, enemies) = await Run(
            dungeon,
            [ApplyDebuff(1_000, targetId: 101, targetInstance: 1)],
            AuraActors);
        var pull = parser.Pulls.ShouldHaveSingleItem();

        enemies.Roster(pull).Select(unit => unit.Key).ShouldBe([new UnitKey(100, 1)]);
        enemies.Population(pull)
            .Where(unit => unit.Key.ActorId == 101)
            .ShouldHaveSingleItem()
            .Origin.ShouldBe(EnemyOrigin.Unrostered);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 5)]
    [InlineData(3, null)]
    [InlineData(3, 1)]
    public async Task Roster_ANpcNotStatingBothBounds_NamesOneInstance(int? minimum, int? maximum)
    {
        var dungeon = Dungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(Id: 1, EncounterId: 0, Kill: null,
                    StartTime: FirstPullStart, EndTime: FirstPullEnd, Name: "Trash",
                    EnemyNpcs:
                    [
                        new DungeonPullNpc(TrashMelee, GameId: 210, minimum, maximum, null, null),
                    ]),
            ],
        };

        var (parser, enemies) = await Run(dungeon, [], DungeonActors);

        enemies.Roster(parser.Pulls.ShouldHaveSingleItem()).Select(unit => unit.Key)
            .ShouldBe([new UnitKey(TrashMelee, minimum ?? 1)]);
    }

    [Fact]
    public async Task Roster_OnADungeonPullNamingNoNpcs_IsEmpty()
    {
        var dungeon = Dungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(Id: 1, EncounterId: 0, Kill: null,
                    StartTime: FirstPullStart, EndTime: FirstPullEnd, Name: "Trash", EnemyNpcs: null),
            ],
        };

        var (parser, enemies) = await Run(dungeon, [], DungeonActors);

        enemies.Roster(parser.Pulls.ShouldHaveSingleItem()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Roster_ExcludesPets()
    {
        var (parser, enemies) = await RunDungeon();

        enemies.IsEnemy(BossPet).ShouldBeFalse();
        enemies.Roster(parser.Pulls[1]).Select(unit => unit.Key).ShouldBe(
            [new UnitKey(TrashCaster, 2), new UnitKey(Boss, 1)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Death_WithNoInstance_ResolvesToTheActorsSoleRosterInstance()
    {
        var (parser, enemies) = await RunDungeon();
        var bossPull = parser.Pulls[1];

        enemies.Resolve(Boss, null, bossPull).ShouldBe(new UnitKey(Boss, 1));

        enemies.DeathsBetween(BossPullStart, BossPullEnd)
            .Where(death => death.Key.ActorId == Boss)
            .ShouldHaveSingleItem()
            .Key.ShouldBe(new UnitKey(Boss, 1));

        enemies.Population(bossPull)
            .Where(unit => unit.Key.ActorId == Boss)
            .ShouldHaveSingleItem()
            .Died.ShouldBe(BossPullEnd);
    }

    [Fact]
    public async Task AliveAt_PullEnd_CountsADeathThereAsAnExit()
    {
        var (parser, enemies) = await RunDungeon();

        enemies.AliveAt(parser.Pulls[1], BossPullEnd - 1).ShouldBe(1);
        enemies.AliveAt(parser.Pulls[1], BossPullEnd).ShouldBe(0);
    }

    [Fact]
    public async Task AliveAt_PullEnd_CountsAUnitWithNoDeathAsDespawning()
    {
        var (parser, enemies) = await RunDungeon();
        var trashPull = parser.Pulls[0];

        enemies.AliveAt(trashPull, FirstPullEnd).ShouldBe(1);
        enemies.Population(trashPull)
            .Where(unit => unit.Died is null)
            .ShouldHaveSingleItem()
            .Key.ShouldBe(new UnitKey(TrashMelee, 2));
    }

    [Fact]
    public async Task AliveAt_RisesAboveTargetCount_WhileAnUnrosteredUnitIsAlive()
    {
        var (parser, enemies) = await RunDungeon();
        var trashPull = parser.Pulls[0];

        enemies.AliveAt(trashPull, 1_050).ShouldBe(enemies.Roster(trashPull).Count);
        enemies.AliveAt(trashPull, 1_200).ShouldBe(5);
        enemies.PeakAlive(trashPull).ShouldBe(5);

        var crystals = enemies.Population(trashPull)
            .Where(unit => unit.Key.ActorId == BloodCrystal)
            .ToList();
        crystals.Count.ShouldBe(2);
        crystals.ShouldAllBe(unit => unit.Origin == EnemyOrigin.Unrostered);
        crystals.ShouldAllBe(unit => unit.Entered == 1_100 && unit.Died == 2_800);
    }

    [Fact]
    public async Task AnUnrosteredUnitKnownOnlyByItsDeath_IsAliveForZeroDuration()
    {
        var (parser, enemies) = await Run(
            Dungeon,
            [Death(2_000, BloodCrystal, 1)],
            DungeonActors);
        var trashPull = parser.Pulls[0];

        var crystal = enemies.Population(trashPull)
            .Where(unit => unit.Key.ActorId == BloodCrystal)
            .ShouldHaveSingleItem();
        crystal.Entered.ShouldBe(2_000);
        crystal.Died.ShouldBe(2_000);

        enemies.AliveAt(trashPull, 1_999).ShouldBe(enemies.Roster(trashPull).Count);
        enemies.AliveAt(trashPull, 2_000).ShouldBe(enemies.Roster(trashPull).Count);
        enemies.PeakAlive(trashPull).ShouldBe(enemies.Roster(trashPull).Count);
    }

    [Fact]
    public async Task AliveAt_ReadsTheSameDuringDispatchAsAfterTheParse()
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        var parser = new ProbingParser(emitter, provider) { Actors = DungeonActors };
        await parser.Analyze(DungeonEvents(), PlayerId, Dungeon);

        var enemies = parser.GetModule<Enemies>()!;
        var samples = parser.GetModule<AliveProbe>()!.Samples;

        samples.Count.ShouldBe(7);
        foreach (var (timestamp, alive) in samples)
            alive.ShouldBe(enemies.AliveAt(timestamp));

        samples.ShouldContain((1_500, 4));
        samples.ShouldContain((2_500, 3));
        samples.ShouldContain((BossPullEnd, 0));
    }

    [Fact]
    public async Task AliveAt_IsUnmovedByAPlayerDeath()
    {
        var (parser, enemies) = await RunDungeon();
        var trashPull = parser.Pulls[0];

        enemies.AliveAt(trashPull, 2_400).ShouldBe(3);
        enemies.AliveAt(trashPull, 2_500).ShouldBe(3);
        enemies.AliveAt(trashPull, 2_600).ShouldBe(3);

        enemies.IsEnemy(PlayerId).ShouldBeFalse();
        enemies.Population(trashPull).ShouldNotContain(unit => unit.Key.ActorId == PlayerId);
        enemies.DeathsBetween(0, 10_000).ShouldNotContain(death => death.Key.ActorId == PlayerId);
    }

    [Fact]
    public async Task AliveAt_IsUnmovedByTheEnvironmentActor()
    {
        var (parser, enemies) = await RunDungeon();
        var trashPull = parser.Pulls[0];

        enemies.IsEnemy(Environment).ShouldBeFalse();
        enemies.AliveAt(trashPull, 1_300).ShouldBe(5);
        enemies.Population(trashPull).ShouldNotContain(unit => unit.Key.ActorId == Environment);
    }

    [Fact]
    public async Task Roster_SplitsAnActorsInstancesAcrossPulls()
    {
        var (parser, enemies) = await RunDungeon();

        var first = enemies.Roster(parser.Pulls[0]).Select(unit => unit.Key).ToHashSet();
        var second = enemies.Roster(parser.Pulls[1]).Select(unit => unit.Key).ToHashSet();

        first.ShouldContain(new UnitKey(TrashCaster, 1));
        second.ShouldContain(new UnitKey(TrashCaster, 2));
        first.Overlaps(second).ShouldBeFalse();
    }

    [Fact]
    public async Task ADeathReachesEveryPullNamingTheUnit()
    {
        var (parser, enemies) = await Run(
            OverlappingRosterDungeon,
            [Death(1_900, Bloodfang, 2), Death(4_000, Bloodfang, 1)],
            OverlappingRosterActors);
        var (first, second) = (parser.Pulls[0], parser.Pulls[1]);

        enemies.Roster(first).Select(unit => unit.Key).ShouldContain(new UnitKey(Bloodfang, 2));
        enemies.Roster(second).Select(unit => unit.Key).ShouldContain(new UnitKey(Bloodfang, 2));

        enemies.Population(second)
            .Where(unit => unit.Key == new UnitKey(Bloodfang, 2))
            .ShouldHaveSingleItem()
            .Died.ShouldBe(1_900);

        enemies.AliveAt(first, 1_899).ShouldBe(2);
        enemies.AliveAt(first, 1_900).ShouldBe(1);
        enemies.AliveAt(first, first.EndTime).ShouldBe(1);

        enemies.AliveAt(second, second.StartTime).ShouldBe(3);
        enemies.AliveAt(second, 4_000).ShouldBe(2);
        enemies.PeakAlive(second).ShouldBe(3);

        enemies.DeathsBetween(0, 10_000).Select(death => death.Key).ShouldBe(
            [new UnitKey(Bloodfang, 2), new UnitKey(Bloodfang, 1)]);
    }

    [Fact]
    public async Task AliveAt_OnASharedPullBoundary_AnswersForTheLaterPull()
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        var parser = new ProbingParser(emitter, provider) { Actors = TouchingPullActors };
        await parser.Analyze([Death(Boundary, TrashMelee, 1)], PlayerId, TouchingPullDungeon);

        var enemies = parser.GetModule<Enemies>()!;
        var (first, second) = (parser.Pulls[0], parser.Pulls[1]);
        first.EndTime.ShouldBe(second.StartTime);

        enemies.Roster(first).Count.ShouldBe(1);
        enemies.Roster(second).Count.ShouldBe(3);

        enemies.AliveAt(Boundary).ShouldBe(enemies.AliveAt(second, Boundary));
        enemies.AliveAt(Boundary).ShouldBe(2);
        enemies.AliveAt(first, Boundary).ShouldBe(1);
        enemies.AliveAt(Boundary).ShouldNotBe(enemies.AliveAt(first, Boundary));

        parser.GetModule<AliveProbe>()!.Samples.ShouldHaveSingleItem()
            .ShouldBe((Boundary, 2));
    }

    [Fact]
    public async Task AliveAt_OutsideEveryPullWindow_IsZero()
    {
        var (parser, enemies) = await RunDungeon();

        enemies.AliveAt(FirstPullStart - 1).ShouldBe(0);
        enemies.AliveAt(4_000).ShouldBe(0);
        enemies.AliveAt(1_050).ShouldBe(3);
        enemies.AliveAt(parser.Pulls[0], 4_000).ShouldBe(0);
    }

    [Fact]
    public async Task AliveBetween_StepsDownAtEachDeath()
    {
        var (parser, enemies) = await RunDungeon();

        var bands = enemies.AliveBetween(parser.Pulls[1], BossPullStart, BossPullEnd);

        bands.Select(band => (band.Start, band.End, band.Alive)).ShouldBe(
        [
            (BossPullStart, 6_000, 2),
            (6_000, BossPullEnd, 1),
            (BossPullEnd, BossPullEnd, 0),
        ]);
    }

    [Fact]
    public async Task DeathsBetween_ReturnsOnlyEnemyDeathsInTheWindow()
    {
        var (_, enemies) = await RunDungeon();

        enemies.DeathsBetween(0, 10_000).Select(death => death.Key).ShouldBe(
        [
            new UnitKey(TrashCaster, 1),
            new UnitKey(TrashMelee, 1),
            new UnitKey(BloodCrystal, 1),
            new UnitKey(BloodCrystal, 2),
            new UnitKey(TrashCaster, 2),
            new UnitKey(Boss, 1),
        ], ignoreOrder: true);

        enemies.DeathsBetween(FirstPullStart, FirstPullEnd).Count.ShouldBe(4);
        enemies.DeathsBetween(0, 1_400).ShouldBeEmpty();
    }

    [Fact]
    public async Task WithAura_CountsDistinctEnemiesAndExcludesAllies()
    {
        var enemies = await RunAura(
            ApplyDebuff(100, targetId: 100, targetInstance: 1),
            ApplyDebuff(100, targetId: 101, targetInstance: 1),
            ApplyDebuff(100, targetId: Ally, targetInstance: null),
            ApplyDebuff(150, targetId: 100, targetInstance: 1));

        enemies.AnyHasAura(EffectId, 200).ShouldBeTrue();
        enemies.CountWithAura(EffectId, 200).ShouldBe(2);
        enemies.WithAura(EffectId, 200).ShouldBe(
            [new UnitKey(100, 1), new UnitKey(101, 1)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task WithAura_RestrictsToTheApplyingSource()
    {
        const int otherSource = 99;

        var enemies = await RunAura(
            ApplyDebuff(100, targetId: 100, targetInstance: 1),
            ApplyDebuff(100, targetId: 101, targetInstance: 1, sourceId: otherSource));

        enemies.CountWithAura(EffectId, 200).ShouldBe(2);
        enemies.CountWithAura(EffectId, 200, sourceId: PlayerId).ShouldBe(1);
        enemies.CountWithAura(EffectId, 200, sourceId: otherSource).ShouldBe(1);
        enemies.AnyHasAura(EffectId, 200, sourceId: 1).ShouldBeFalse();
    }

    [Fact]
    public async Task MergedAuraWindows_CollapsesOverlapAndKeepsDisjointWindowsApart()
    {
        var enemies = await RunAura(
            ApplyDebuff(100, targetId: 100, targetInstance: 1),
            RemoveDebuff(500, targetId: 100, targetInstance: 1),
            ApplyDebuff(400, targetId: 101, targetInstance: 1),
            RemoveDebuff(800, targetId: 101, targetInstance: 1),
            ApplyDebuff(2_000, targetId: 100, targetInstance: 1),
            RemoveDebuff(2_500, targetId: 100, targetInstance: 1),
            ApplyDebuff(3_000, targetId: 101, targetInstance: 1),
            RemoveDebuff(3_500, targetId: 101, targetInstance: 1));

        enemies.AuraWindows(EffectId, 0, 4_000).Count.ShouldBe(4);
        enemies.MergedAuraWindows(EffectId, 0, 4_000).ShouldBe(
        [
            new AuraWindow(100, 800),
            new AuraWindow(2_000, 2_500),
            new AuraWindow(3_000, 3_500),
        ]);
    }

    [Fact]
    public async Task MergedAuraWindows_AcrossEffectIds_ProducesOneTimeline()
    {
        const int secondEffectId = EffectId + 1;

        var enemies = await RunAura(
            ApplyDebuff(100, targetId: 100, targetInstance: 1),
            RemoveDebuff(500, targetId: 100, targetInstance: 1),
            ApplyDebuff(450, targetId: 101, targetInstance: 1, effectId: secondEffectId),
            RemoveDebuff(900, targetId: 101, targetInstance: 1, effectId: secondEffectId));

        enemies.MergedAuraWindows([EffectId, secondEffectId], 0, 4_000)
            .ShouldBe([new AuraWindow(100, 900)]);
    }

    [Fact]
    public async Task AuraTargetsBetween_ReportsTheConcurrentEnemyCount()
    {
        var enemies = await RunAura(
            ApplyDebuff(100, targetId: 100, targetInstance: 1),
            RemoveDebuff(500, targetId: 100, targetInstance: 1),
            ApplyDebuff(400, targetId: 101, targetInstance: 1),
            RemoveDebuff(800, targetId: 101, targetInstance: 1),
            ApplyDebuff(400, targetId: Ally, targetInstance: null),
            RemoveDebuff(800, targetId: Ally, targetInstance: null));

        enemies.AuraTargetsBetween(EffectId, 0, 1_000)
            .Select(band => (band.Start, band.End, band.Targets))
            .ShouldBe(
            [
                (0, 100, 0),
                (100, 400, 1),
                (400, 501, 2),
                (501, 801, 1),
                (801, 1_000, 0),
            ]);

        enemies.PeakAuraTargets(EffectId, 0, 1_000).ShouldBe(2);
        enemies.PeakAuraTargets(EffectId, 0, 200).ShouldBe(1);
    }

    [Fact]
    public async Task AuraTargetsBetween_CountsConcurrentApplicationsOnOneEnemyOnce()
    {
        var enemies = await RunAura(
            ApplyDebuff(100, targetId: 100, targetInstance: 1),
            ApplyDebuff(200, targetId: 100, targetInstance: 1),
            ApplyDebuff(300, targetId: 100, targetInstance: 1));

        enemies.AuraWindows(EffectId, 0, 1_000).Count.ShouldBe(3);
        enemies.PeakAuraTargets(EffectId, 0, 1_000).ShouldBe(1);
    }

    [Fact]
    public async Task NoActors_KeepsTheRosterAndAdmitsNothingElse()
    {
        var (parser, enemies) = await Run(Dungeon, DungeonEvents(), actors: []);
        var trashPull = parser.Pulls[0];

        enemies.IsEnemy(Boss).ShouldBeFalse();
        enemies.Roster(trashPull).Count.ShouldBe(enemies.Roster(trashPull).Count);
        enemies.Population(trashPull).ShouldBe(enemies.Roster(trashPull));
        enemies.Population(trashPull).ShouldAllBe(unit => unit.Died == null);
        enemies.AliveAt(trashPull, 1_200).ShouldBe(enemies.Roster(trashPull).Count);
        enemies.DeathsBetween(0, 10_000).ShouldBeEmpty();
        enemies.CountWithAura(EffectId, 1_200).ShouldBe(0);
    }

    private const int EffectId = 1002325;

    private static readonly Ability Hit = new() { FSLID = 4242, Name = "Hit" };

    private static async Task<(CombatLogParser Parser, Enemies Enemies)> RunDungeon() =>
        await Run(Dungeon, DungeonEvents(), DungeonActors);

    private static async Task<Enemies> RunAura(params Event[] events)
    {
        var (_, enemies) = await Run(SinglePullDungeon, [.. events], AuraActors);
        return enemies;
    }

    private static async Task<(CombatLogParser Parser, Enemies Enemies)> Run(
        ReportDungeon dungeon,
        List<Event> events,
        List<ReportActor> actors)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        var parser = new TestParser(emitter, provider) { Actors = actors };
        await parser.Analyze(events, PlayerId, dungeon);
        return (parser, parser.GetModule<Enemies>()!);
    }

    private static DamageEvent Damage(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = Hit,
    };

    private static DeathEvent Death(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = Hit,
    };

    private static ApplyDebuffEvent ApplyDebuff(
        int timestamp, int targetId, int? targetInstance, int sourceId = PlayerId, int effectId = EffectId) => new()
        {
            Timestamp = timestamp,
            SourceId = sourceId,
            TargetId = targetId,
            TargetInstance = targetInstance,
            Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
        };

    private static RemoveDebuffEvent RemoveDebuff(
        int timestamp, int targetId, int? targetInstance, int sourceId = PlayerId, int effectId = EffectId) => new()
        {
            Timestamp = timestamp,
            SourceId = sourceId,
            TargetId = targetId,
            TargetInstance = targetInstance,
            Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
        };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [typeof(Combatants), typeof(Enemies)];

        protected override Type[] GetNormalizerTypes() => [typeof(PullBookendNormalizer), typeof(DungeonBookendNormalizer)];
    }

    private sealed class ProbingParser(EventEmitter emitter, IServiceProvider provider)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [typeof(Combatants), typeof(Enemies), typeof(AliveProbe)];

        protected override Type[] GetNormalizerTypes() => [typeof(PullBookendNormalizer), typeof(DungeonBookendNormalizer)];

        protected override object? CreateInstance(Type type) =>
            type == typeof(AliveProbe) ? new AliveProbe() : base.CreateInstance(type);
    }

    private sealed partial class AliveProbe : Analyzer
    {
        public List<(int Timestamp, int Alive)> Samples { get; } = [];

        [On<DeathEvent>]
        private void OnDeath(DeathEvent deathEvent) =>
            Samples.Add((deathEvent.Timestamp, Owner.Enemies!.AliveAt(deathEvent.Timestamp)));
    }
}
