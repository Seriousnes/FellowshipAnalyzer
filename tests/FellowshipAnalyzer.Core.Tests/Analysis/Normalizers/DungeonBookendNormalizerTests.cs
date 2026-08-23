using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class DungeonBookendNormalizerTests
{
    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 100, EndTime: 5000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static readonly FullCombatant EmptyCombatant = new(new CombatantInfoEvent());

    [Fact]
    public void Normalize_PrependsDungeonStartAndAppendsDungeonEnd()
    {
        var ctx = new ParseContext(PlayerId: 1, Dungeon: Dungeon, ActorNames: [], EmptyCombatant);
        var normalizer = new DungeonBookendNormalizer(ctx);
        var existing = new ApplyBuffEvent { Timestamp = 200, SourceId = 1, TargetId = 1 };

        var result = normalizer.Normalize([existing], playerId: 1);

        Assert.Equal(3, result.Count);
        var start = Assert.IsType<DungeonStartEvent>(result[0]);
        Assert.Same(existing, result[1]);
        var end = Assert.IsType<DungeonEndEvent>(result[2]);
        Assert.Equal(100, start.Timestamp);
        Assert.Equal(5000, end.Timestamp);
    }

    [Fact]
    public void Normalize_PreservesOrderOfExistingEvents()
    {
        var ctx = new ParseContext(PlayerId: 1, Dungeon: Dungeon, ActorNames: [], EmptyCombatant);
        var normalizer = new DungeonBookendNormalizer(ctx);
        var first = new ApplyBuffEvent { Timestamp = 200 };
        var second = new RemoveBuffEvent { Timestamp = 300 };

        var result = normalizer.Normalize([first, second], playerId: 1);

        Assert.Equal(4, result.Count);
        Assert.IsType<DungeonStartEvent>(result[0]);
        Assert.Same(first, result[1]);
        Assert.Same(second, result[2]);
        Assert.IsType<DungeonEndEvent>(result[3]);
    }
}
