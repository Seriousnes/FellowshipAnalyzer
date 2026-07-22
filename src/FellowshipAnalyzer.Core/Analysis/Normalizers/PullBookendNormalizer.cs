using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Fabricates a <see cref="PullStartEvent"/> / <see cref="PullEndEvent"/> pair for each Fellowship
/// Logs dungeon pull on the fight, classifying it from the pull's <c>encounterID</c>, <c>kill</c>,
/// and <c>enemyNPCs</c>. A fight that exposes no dungeon pulls (raids and other non-dungeon content)
/// gets one implicit pull spanning the whole fight, classified from the fight's own fields. Pull opens
/// are placed ahead of the stream and closes after it so the stable dispatch sort seats each boundary
/// against same-timestamp gameplay (opens before, closes after); running before
/// <see cref="FightBookendNormalizer"/> lets the fight bookends wrap the pull bookends.
/// </summary>
public sealed class PullBookendNormalizer(ParseContext parseContext) : IEventNormalizer
{
    public int Priority => -1000;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var pulls = BuildPulls();

        var result = new List<Event>(events.Count + (pulls.Count * 2));
        foreach (var pull in pulls)
            result.Add(new PullStartEvent { Timestamp = pull.StartTime, Pull = pull });

        result.AddRange(events);

        foreach (var pull in pulls)
            result.Add(new PullEndEvent { Timestamp = pull.EndTime, Pull = pull });

        return result;
    }

    private List<Pull> BuildPulls()
    {
        var dungeonPulls = parseContext.DungeonPulls;
        if (dungeonPulls is { Count: > 0 })
        {
            var pulls = new List<Pull>(dungeonPulls.Count);
            for (var i = 0; i < dungeonPulls.Count; i++)
                pulls.Add(FromDungeonPull(i, dungeonPulls[i]));
            return pulls;
        }

        return [FromFight(parseContext.Fight)];
    }

    private static Pull FromDungeonPull(int index, DungeonPull pull)
    {
        var targetCount = CountTargets(pull.EnemyNpcs);
        var isBoss = pull.EncounterId != 0;
        return new Pull(
            Index: index,
            Id: pull.Id,
            Name: pull.Name,
            StartTime: (int)pull.StartTime,
            EndTime: (int)pull.EndTime,
            Targets: ShapeFor(isBoss),
            IsBoss: isBoss,
            Kill: pull.Kill ?? false,
            TargetCount: targetCount);
    }

    private static Pull FromFight(ReportFight fight)
    {
        var targetCount = CountTargets(fight.EnemyNpcs);
        var isBoss = fight.EncounterId != 0;
        return new Pull(
            Index: 0,
            Id: 0,
            Name: fight.Name,
            StartTime: (int)fight.StartTime,
            EndTime: (int)fight.EndTime,
            Targets: ShapeFor(isBoss),
            IsBoss: isBoss,
            Kill: fight.Kill ?? false,
            TargetCount: targetCount);
    }

    private static PullKind ShapeFor(bool isBoss)
        => isBoss ? PullKind.Single : PullKind.Multi;

    private static int CountTargets(IReadOnlyList<DungeonPullNpc>? npcs)
    {
        if (npcs is null) return 0;

        var total = 0;
        foreach (var npc in npcs)
            total += InstanceSpan(npc.MinimumInstanceId, npc.MaximumInstanceId);
        return total;
    }

    private static int CountTargets(IReadOnlyList<FightNpc>? npcs)
    {
        if (npcs is null) return 0;

        var total = 0;
        foreach (var npc in npcs)
        {
            if (npc.PetOwner is not null) continue;
            total += npc.InstanceCount ?? 1;
        }
        return total;
    }

    private static int InstanceSpan(int? minimumInstanceId, int? maximumInstanceId)
        => minimumInstanceId is int lo && maximumInstanceId is int hi && hi >= lo
            ? (hi - lo) + 1
            : 1;
}
