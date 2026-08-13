using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class PullBookendNormalizerTests
{
    private static readonly FullCombatant EmptyCombatant = new(new CombatantInfoEvent());

    private static ParseContext Context(ReportDungeon dungeon)
        => new(PlayerId: 1, Dungeon: dungeon, ActorNames: new Dictionary<int, string>(), EmptyCombatant);

    private static ReportDungeon Dungeon(
        int encounterId = 0,
        bool? kill = null,
        double startTime = 100,
        double endTime = 5000,
        IReadOnlyList<DungeonPull>? dungeonPulls = null,
        IReadOnlyList<DungeonNpc>? enemyNpcs = null,
        string name = "Dungeon")
        => new(Id: 0, Name: name, EncounterId: encounterId, Kill: kill,
            StartTime: startTime, EndTime: endTime, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null, InProgress: false,
            DungeonPulls: dungeonPulls, EnemyNpcs: enemyNpcs);

    private static List<PullStartEvent> Starts(List<Event> events) => [.. events.OfType<PullStartEvent>()];

    private static List<PullEndEvent> Ends(List<Event> events) => [.. events.OfType<PullEndEvent>()];

    [Fact]
    public void Normalize_NoDungeonPulls_FabricatesOneImplicitPullSpanningDungeon()
    {
        var normalizer = new PullBookendNormalizer(Context(Dungeon(startTime: 100, endTime: 5000)));
        var existing = new ApplyBuffEvent { Timestamp = 200 };

        var result = normalizer.Normalize([existing], playerId: 1);

        var start = Assert.Single(Starts(result));
        var end = Assert.Single(Ends(result));
        Assert.Same(start, end.Start);
        Assert.Same(end, start.End);
        Assert.Equal(0, start.Index);
        Assert.Equal(100, start.Timestamp);
        Assert.Equal(5000, end.Timestamp);
        Assert.Equal(100, start.StartTime);
        Assert.Equal(5000, start.EndTime);
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
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));

        var result = normalizer.Normalize([], playerId: 1);

        var starts = Starts(result);
        var ends = Ends(result);
        Assert.Equal(2, starts.Count);
        Assert.Equal(2, ends.Count);
        Assert.Equal([0, 1], starts.Select(s => s.Index));
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
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1)));

        Assert.Equal(expectedBoss, pull.IsBoss);
    }

    [Fact]
    public void Classify_WipePull_IsEmittedWithKillFalse()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 42, Kill: false, StartTime: 200, EndTime: 800, Name: "Wipe", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1)));

        Assert.False(pull.Kill);
        Assert.True(pull.IsBoss);
    }

    [Theory]
    [InlineData(42, PullKind.Single)]
    [InlineData(0, PullKind.Multi)]
    public void Classify_Shape_FromEncounterId(int encounterId, PullKind expectedShape)
    {
        var npcs = new List<DungeonPullNpc>
        {
            new(Id: 10, GameId: 100, MinimumInstanceId: 1, MaximumInstanceId: 3,
                MinimumInstanceGroupId: null, MaximumInstanceGroupId: null),
        };
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: encounterId, Kill: true, StartTime: 200, EndTime: 800, Name: "P", EnemyNpcs: npcs),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1)));

        Assert.Equal(expectedShape, pull.Targets);
    }

    [Fact]
    public void Normalize_ReturnsAnAscendingStream()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: true, StartTime: 200, EndTime: 800, Name: "Trash", EnemyNpcs: null),
            new(Id: 2, EncounterId: 42, Kill: true, StartTime: 800, EndTime: 2000, Name: "Boss", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));

        var result = normalizer.Normalize(
        [
            new ApplyBuffEvent { Timestamp = 100 },
            new ApplyBuffEvent { Timestamp = 200 },
            new ApplyBuffEvent { Timestamp = 800 },
            new ApplyBuffEvent { Timestamp = 2000 },
            new ApplyBuffEvent { Timestamp = 3000 },
        ], playerId: 1);

        var timestamps = result.Select(e => e.Timestamp).ToList();
        Assert.Equal(timestamps.Order(), timestamps);
    }

    [Fact]
    public void Normalize_SeatsAnOpenBeforeSameTimestampGameplay()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: true, StartTime: 200, EndTime: 800, Name: "Trash", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));
        var atStart = new ApplyBuffEvent { Timestamp = 200 };

        var result = normalizer.Normalize(
            [new ApplyBuffEvent { Timestamp = 100 }, atStart], playerId: 1);

        Assert.True(
            result.FindIndex(e => e is PullStartEvent) < result.IndexOf(atStart),
            "an open must precede same-timestamp gameplay");
    }

    [Fact]
    public void Normalize_SeatsACloseAfterSameTimestampGameplay()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: true, StartTime: 200, EndTime: 800, Name: "Trash", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));
        var atEnd = new ApplyBuffEvent { Timestamp = 800 };

        var result = normalizer.Normalize(
            [atEnd, new ApplyBuffEvent { Timestamp = 900 }], playerId: 1);

        Assert.True(
            result.IndexOf(atEnd) < result.FindIndex(e => e is PullEndEvent),
            "a close must follow same-timestamp gameplay");
    }

    [Fact]
    public void Normalize_SeatsAnOpenBeforeACloseSharingItsTimestamp()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: true, StartTime: 200, EndTime: 800, Name: "First", EnemyNpcs: null),
            new(Id: 2, EncounterId: 0, Kill: true, StartTime: 800, EndTime: 1400, Name: "Second", EnemyNpcs: null),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(dungeonPulls: pulls)));

        var result = normalizer.Normalize([], playerId: 1);

        var secondOpen = result.FindIndex(e => e is PullStartEvent { Index: 1 });
        var firstClose = result.FindIndex(e => e is PullEndEvent { Start.Index: 0 });
        Assert.True(secondOpen < firstClose, "an open must precede a close sharing its timestamp");
    }

    [Fact]
    public void Classify_ImplicitPull_TakesItsEncounterAndKillFromTheDungeon()
    {
        var npcs = new List<DungeonNpc>
        {
            new(Id: 10, GameId: 100, InstanceCount: 1, GroupCount: 1, PetOwner: null),
            new(Id: 11, GameId: 101, InstanceCount: 5, GroupCount: 1, PetOwner: 10),
        };
        var normalizer = new PullBookendNormalizer(Context(Dungeon(encounterId: 7, kill: true, enemyNpcs: npcs)));

        var pull = Assert.Single(Starts(normalizer.Normalize([], playerId: 1)));

        Assert.Equal(PullKind.Single, pull.Targets);
        Assert.True(pull.IsBoss);
        Assert.True(pull.Kill);
    }
}
