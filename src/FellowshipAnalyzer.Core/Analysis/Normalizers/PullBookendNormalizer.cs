using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Fabricates a <see cref="PullStartEvent"/> and its <see cref="PullEndEvent"/> for each Fellowship
/// Logs dungeon pull on the dungeon, classifying it from the pull's <c>encounterID</c>, <c>kill</c>,
/// and <c>enemyNPCs</c>. A dungeon that exposes no dungeon pulls (raids and other non-dungeon content)
/// gets one implicit pull spanning the whole dungeon, classified from the dungeon's own fields. Pull opens
/// are placed ahead of the stream and closes after it so the stable dispatch sort seats each boundary
/// against same-timestamp gameplay (opens before, closes after); running before
/// <see cref="DungeonBookendNormalizer"/> lets the dungeon bookends wrap the pull bookends.
/// <para>
/// A pull carries only what its own Fellowship Logs entry states. How many enemies it contains is
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

        var result = new List<Event>(events.Count + (pulls.Count * 2));
        result.AddRange(pulls);
        result.AddRange(events);
        foreach (var pull in pulls)
            result.Add(pull.End);

        return result;
    }

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
