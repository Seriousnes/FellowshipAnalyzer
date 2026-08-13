using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class CombatLogParserTests
{
    [Fact]
    public async Task Analyze_WithPullNormalizer_NestsImplicitPullInsideDungeonBookends()
    {
        var owner = CreateCombatLogParser(
            normalizerTypes: [typeof(PullBookendNormalizer), typeof(DungeonBookendNormalizer)]);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);

        var events = owner.Events;
        Assert.IsType<DungeonStartEvent>(events[0]);
        var pullStart = Assert.IsType<PullStartEvent>(events[1]);
        Assert.IsType<DungeonEndEvent>(events[^1]);
        var pullEnd = Assert.IsType<PullEndEvent>(events[^2]);

        Assert.Same(pullStart.Pull, pullEnd.Pull);
        Assert.Equal(owner.DungeonStartTime, pullStart.Timestamp);
        Assert.Equal(owner.DungeonEndTime, pullEnd.Timestamp);
        Assert.Single(events.OfType<PullStartEvent>());
        Assert.Single(events.OfType<PullEndEvent>());
    }

    [Fact]
    public async Task Analyze_NestsPullBoundariesAroundGameplayAtSharedTimestamps()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: DungeonStart, abilityId: 1),
            CreateCast(timestamp: 30_000, abilityId: 1),
            CreateCast(timestamp: DungeonEnd, abilityId: 1),
        };

        var owner = CreateCombatLogParser(
            normalizerTypes: [typeof(PullBookendNormalizer), typeof(DungeonBookendNormalizer)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var stream = owner.Events;
        var pullStart = stream.FindIndex(e => e is PullStartEvent);
        var pullEnd = stream.FindIndex(e => e is PullEndEvent);
        var castAtStart = stream.FindIndex(e => e is CastEvent c && c.Timestamp == DungeonStart);
        var castAtEnd = stream.FindIndex(e => e is CastEvent c && c.Timestamp == DungeonEnd);

        Assert.True(pullStart < castAtStart, "an open must precede same-timestamp gameplay");
        Assert.True(castAtEnd < pullEnd, "a close must follow same-timestamp gameplay");
    }

    [Fact]
    public async Task Analyze_PreservesInputOrderForEqualTimestampEvents()
    {
        var events = new List<Event>();
        for (var i = 0; i < 30; i++)
            events.Add(CreateCast(timestamp: 500, abilityId: 1000 + i));

        var owner = CreateCombatLogParser(normalizerTypes: [typeof(DungeonBookendNormalizer)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var ids = owner.Events
            .OfType<CastEvent>()
            .Where(c => c.Timestamp == 500)
            .Select(c => c.Ability.Id)
            .ToList();

        Assert.Equal(Enumerable.Range(1000, 30).ToList(), ids);
    }

    private const int DungeonStart = 0;
    private const int DungeonEnd = 60_000;
}
