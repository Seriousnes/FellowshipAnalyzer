using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class PullBookendNormalizerTests
{
    private static readonly Combatant EmptyCombatant = new(new CombatantInfoEvent());

    private static ParseContext Context(ReportFight fight)
        => new(PlayerId: 1, Fight: fight, ActorNames: new Dictionary<int, string>(), EmptyCombatant);

    private static ReportFight Fight(
        int encounterId = 0,
        bool? kill = null,
        double startTime = 100,
        double endTime = 5000,
        IReadOnlyList<DungeonPull>? dungeonPulls = null,
        IReadOnlyList<FightNpc>? enemyNpcs = null,
        string name = "Fight")
        => new(Id: 0, Name: name, EncounterId: encounterId, Kill: kill,
            StartTime: startTime, EndTime: endTime, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null, InProgress: false,
            DungeonPulls: dungeonPulls, EnemyNpcs: enemyNpcs);

    private static List<PullStartEvent> Starts(List<Event> events) => [.. events.OfType<PullStartEvent>()];

    private static List<PullEndEvent> Ends(List<Event> events) => [.. events.OfType<PullEndEvent>()];

    [Fact]
    public void Normalize_NoDungeonPulls_FabricatesOneImplicitPullSpanningFight()
    {
        var normalizer = new PullBookendNormalizer(Context(Fight(startTime: 100, endTime: 5000)));
        var existing = new ApplyBuffEvent { Timestamp = 200 };

        var result = normalizer.Normalize([existing], playerId: 1);

        var start = Assert.Single(Starts(result));
        var end = Assert.Single(Ends(result));
        Assert.Same(start.Pull, end.Pull);
        Assert.Equal(0, start.Pull.Index);
        Assert.Equal(100, start.Timestamp);
        Assert.Equal(5000, end.Timestamp);
        Assert.Equal(100, start.Pull.StartTime);
        Assert.Equal(5000, start.Pull.EndTime);
        Assert.Contains(existing, result);
    }

    [Fact]
    public void Normalize_DungeonPulls_FabricatesOnePairPerPullInOrder()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 200, EndTime: 800, Name: "Trash", EnemyNpcs: null),
            new(Id: 2, EncounterId: 42, Kill: true, StartTime: 1000, EndTime: 2000, Name: "Boss", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Fight(dungeonPulls: pulls)));

        var result = normalizer.Normalize([], playerId: 1);

        var starts = Starts(result);
        var ends = Ends(result);
        Assert.Equal(2, starts.Count);
        Assert.Equal(2, ends.Count);
        Assert.Equal([0, 1], starts.Select(s => s.Pull.Index));
        Assert.Equal([200, 1000], starts.Select(s => s.Timestamp));
        Assert.Equal([800, 2000], ends.Select(e => e.Timestamp));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(42, true)]
    public void Classify_IsBoss_FromEncounterId(int encounterId, bool expectedBoss)
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: encounterId, Kill: true, StartTime: 200, EndTime: 800, Name: "P", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Fight(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1))).Pull;

        Assert.Equal(expectedBoss, pull.IsBoss);
    }

    [Fact]
    public void Classify_WipePull_IsEmittedWithKillFalse()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 42, Kill: false, StartTime: 200, EndTime: 800, Name: "Wipe", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Fight(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1))).Pull;

        Assert.False(pull.Kill);
        Assert.True(pull.IsBoss);
    }

    [Fact]
    public void Classify_SingleTarget_FromOneEnemyInstance()
    {
        var npcs = new List<DungeonPullNpc>
        {
            new(Id: 10, GameId: 100, MinimumInstanceId: 1, MaximumInstanceId: 1,
                MinimumInstanceGroupId: null, MaximumInstanceGroupId: null),
        };
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 42, Kill: true, StartTime: 200, EndTime: 800, Name: "Boss", EnemyNpcs: npcs),
        };
        var normalizer = new PullBookendNormalizer(Context(Fight(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1))).Pull;

        Assert.Equal(1, pull.TargetCount);
        Assert.Equal(PullKind.Single, pull.Targets);
    }

    [Fact]
    public void Classify_MultiTarget_FromInstanceRangeAndMultipleNpcs()
    {
        var npcs = new List<DungeonPullNpc>
        {
            new(Id: 10, GameId: 100, MinimumInstanceId: 1, MaximumInstanceId: 3,
                MinimumInstanceGroupId: null, MaximumInstanceGroupId: null),
            new(Id: 11, GameId: 101, MinimumInstanceId: null, MaximumInstanceId: null,
                MinimumInstanceGroupId: null, MaximumInstanceGroupId: null),
        };
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 200, EndTime: 800, Name: "Trash", EnemyNpcs: npcs),
        };
        var normalizer = new PullBookendNormalizer(Context(Fight(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1))).Pull;

        Assert.Equal(4, pull.TargetCount);
        Assert.Equal(PullKind.Multi, pull.Targets);
    }

    [Fact]
    public void Classify_ImplicitPull_TargetsFromFightNpcs_ExcludingPets()
    {
        var npcs = new List<FightNpc>
        {
            new(Id: 10, GameId: 100, InstanceCount: 1, GroupCount: 1, PetOwner: null),
            new(Id: 11, GameId: 101, InstanceCount: 5, GroupCount: 1, PetOwner: 10),
        };
        var normalizer = new PullBookendNormalizer(Context(Fight(encounterId: 7, kill: true, enemyNpcs: npcs)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1))).Pull;

        Assert.Equal(1, pull.TargetCount);
        Assert.Equal(PullKind.Single, pull.Targets);
        Assert.True(pull.IsBoss);
        Assert.True(pull.Kill);
    }
}
