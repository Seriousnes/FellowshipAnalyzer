using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Fabricates a <see cref="PullStartEvent"/> and its <see cref="PullEndEvent"/> for each Fellowship
/// Logs dungeon pull on the dungeon, classifying it from the pull's <c>encounterID</c>, <c>kill</c>,
/// and <c>enemyNPCs</c>. A dungeon that exposes no dungeon pulls (raids and other non-dungeon content)
/// gets one implicit pull spanning the whole dungeon, classified from the dungeon's own fields.
/// <para>
/// Boundaries are merged into the incoming stream by timestamp, so what comes back is still ascending: an
/// open is seated before the first event at or after its timestamp, a close after the last event at or
/// before its timestamp, and an open sharing a timestamp with a close precedes it. A pull that starts
/// where another ends therefore opens before the earlier pull's close is reached, and
/// <see cref="CombatLogParser.BeginPull"/> closes the open pull first. Running before
/// <see cref="DungeonBookendNormalizer"/> lets the dungeon bookends wrap the pull bookends.
/// </para>
/// <para>
/// A pull has only what its own Fellowship Logs entry states. How many enemies it contains is
/// <see cref="Enemies.Roster"/>'s answer, projected out of the seeded population when something asks,
/// so no roster is expanded here and no count can drift from the units it counts.
/// </para>
/// </summary>
public sealed class PullBookendNormalizer(ParseContext parseContext) : IEventNormalizer
{
    /// <inheritdoc/>
    public int Priority => -1000;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var pulls = BuildPulls();
        var boundaries = Ordered(pulls);

        var result = new List<Event>(events.Count + boundaries.Count);
        var next = 0;

        foreach (var e in events)
        {
            while (next < boundaries.Count && Precedes(boundaries[next], e.Timestamp))
                result.Add(boundaries[next++]);

            result.Add(e);
        }

        for (; next < boundaries.Count; next++)
            result.Add(boundaries[next]);

        return result;
    }

    private static List<Event> Ordered(List<PullStartEvent> pulls)
    {
        var boundaries = new List<Event>(pulls.Count * 2);
        foreach (var pull in pulls)
        {
            boundaries.Add(pull);
            boundaries.Add(pull.End);
        }

        return [.. boundaries
            .OrderBy(static boundary => boundary.Timestamp)
            .ThenBy(static boundary => boundary is PullStartEvent ? 0 : 1)];
    }

    private static bool Precedes(Event boundary, int timestamp) =>
        boundary is PullStartEvent
            ? boundary.Timestamp <= timestamp
            : boundary.Timestamp < timestamp;

    private List<PullStartEvent> BuildPulls()
    {
        var dungeonPulls = parseContext.DungeonPulls;
        if (dungeonPulls is { Count: > 0 })
        {
            var pulls = new List<PullStartEvent>(dungeonPulls.Count);
            for (var i = 0; i < dungeonPulls.Count; i++)
                pulls.Add(FromDungeonPull(i, dungeonPulls[i]));
            return pulls;
        }

        return [FromDungeon(parseContext.Dungeon)];
    }

    private static PullStartEvent FromDungeonPull(int index, DungeonPull pull)
    {
        var isBoss = pull.EncounterId != 0;
        return new PullStartEvent
        {
            Timestamp = (int)pull.StartTime,
            End = new PullEndEvent { Timestamp = (int)pull.EndTime },
            Index = index,
            Id = pull.Id,
            Name = pull.Name,
            Targets = ShapeFor(isBoss),
            IsBoss = isBoss,
            Kill = pull.Kill ?? false,
        };
    }

    private static PullStartEvent FromDungeon(ReportDungeon dungeon)
    {
        var isBoss = dungeon.EncounterId != 0;
        return new PullStartEvent
        {
            Timestamp = (int)dungeon.StartTime,
            End = new PullEndEvent { Timestamp = (int)dungeon.EndTime },
            Index = 0,
            Id = 0,
            Name = dungeon.Name,
            Targets = ShapeFor(isBoss),
            IsBoss = isBoss,
            Kill = dungeon.Kill ?? false,
        };
    }

    private static PullKind ShapeFor(bool isBoss)
        => isBoss ? PullKind.Single : PullKind.Multi;
}
