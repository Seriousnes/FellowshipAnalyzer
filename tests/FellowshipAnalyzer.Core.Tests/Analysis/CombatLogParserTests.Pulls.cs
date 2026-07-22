using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class CombatLogParserTests
{
    [Fact]
    public async Task Analyze_WithPullNormalizer_NestsImplicitPullInsideFightBookends()
    {
        var owner = CreateCombatLogParser(
            normalizerTypes: [typeof(PullBookendNormalizer), typeof(FightBookendNormalizer)]);

        await owner.Analyze(CreateEvents(), playerId: 7, fight: TestFight);

        var events = owner.Events;
        Assert.IsType<FightStartEvent>(events[0]);
        var pullStart = Assert.IsType<PullStartEvent>(events[1]);
        Assert.IsType<FightEndEvent>(events[^1]);
        var pullEnd = Assert.IsType<PullEndEvent>(events[^2]);

        Assert.Same(pullStart.Pull, pullEnd.Pull);
        Assert.Equal(owner.FightStartTime, pullStart.Timestamp);
        Assert.Equal(owner.FightEndTime, pullEnd.Timestamp);
        Assert.Single(events.OfType<PullStartEvent>());
        Assert.Single(events.OfType<PullEndEvent>());
    }

    [Fact]
    public async Task Analyze_NestsPullBoundariesAroundGameplayAtSharedTimestamps()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: FightStart, abilityId: 1),
            CreateCast(timestamp: 30_000, abilityId: 1),
            CreateCast(timestamp: FightEnd, abilityId: 1),
        };

        var owner = CreateCombatLogParser(
            normalizerTypes: [typeof(PullBookendNormalizer), typeof(FightBookendNormalizer)]);

        await owner.Analyze(events, playerId: 7, fight: TestFight);

        var stream = owner.Events;
        var pullStart = stream.FindIndex(e => e is PullStartEvent);
        var pullEnd = stream.FindIndex(e => e is PullEndEvent);
        var castAtStart = stream.FindIndex(e => e is CastEvent c && c.Timestamp == FightStart);
        var castAtEnd = stream.FindIndex(e => e is CastEvent c && c.Timestamp == FightEnd);

        Assert.True(pullStart < castAtStart, "an open must precede same-timestamp gameplay");
        Assert.True(castAtEnd < pullEnd, "a close must follow same-timestamp gameplay");
    }

    [Fact]
    public async Task Analyze_PreservesInputOrderForEqualTimestampEvents()
    {
        var events = new List<Event>();
        for (var i = 0; i < 30; i++)
            events.Add(CreateCast(timestamp: 500, abilityId: 1000 + i));

        var owner = CreateCombatLogParser(normalizerTypes: [typeof(FightBookendNormalizer)]);

        await owner.Analyze(events, playerId: 7, fight: TestFight);

        var ids = owner.Events
            .OfType<CastEvent>()
            .Where(c => c.Timestamp == 500)
            .Select(c => c.Ability.Id)
            .ToList();

        Assert.Equal(Enumerable.Range(1000, 30).ToList(), ids);
    }

    private const int FightStart = 0;
    private const int FightEnd = 60_000;
}
