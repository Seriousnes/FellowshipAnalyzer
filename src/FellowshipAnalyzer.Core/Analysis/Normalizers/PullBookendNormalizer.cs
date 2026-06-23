using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Fabricates a <see cref="PullStartEvent"/> / <see cref="PullEndEvent"/> pair for each Fellowship
/// Logs dungeon pull on the fight, classifying it from the pull's <c>encounterID</c>, <c>kill</c>,
/// and <c>enemyNPCs</c>. A fight that exposes no dungeon pulls (raids and other non-dungeon content)
/// gets one implicit pull spanning the whole fight, classified from the fight's own fields. The
/// dispatch sort (see <see cref="EventDispatchOrder"/>) nests these inside the fight bookends.
/// </summary>
public sealed class PullBookendNormalizer(ParseContext parseContext) : IEventNormalizer
{
    private const int SingleTargetThreshold = 1;

    public int Priority => -999;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var pulls = BuildPulls();

        var result = new List<Event>(events.Count + (pulls.Count * 2));
        result.AddRange(events);

        foreach (var pull in pulls)
        {
            result.Add(new PullStartEvent { Timestamp = pull.StartTime, Pull = pull });
            result.Add(new PullEndEvent { Timestamp = pull.EndTime, Pull = pull });
        }

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
        return new Pull(
            Index: index,
            Name: pull.Name,
            StartTime: (int)pull.StartTime,
            EndTime: (int)pull.EndTime,
            Targets: ShapeFor(targetCount),
            IsBoss: pull.EncounterId != 0,
            Kill: pull.Kill ?? false,
            TargetCount: targetCount);
    }

    private static Pull FromFight(ReportFight fight)
    {
        var targetCount = CountTargets(fight.EnemyNpcs);
        return new Pull(
            Index: 0,
            Name: fight.Name,
            StartTime: (int)fight.StartTime,
            EndTime: (int)fight.EndTime,
            Targets: ShapeFor(targetCount),
            IsBoss: fight.EncounterId != 0,
            Kill: fight.Kill ?? false,
            TargetCount: targetCount);
    }

    private static PullKind ShapeFor(int targetCount)
        => targetCount > SingleTargetThreshold ? PullKind.Multi : PullKind.Single;

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
