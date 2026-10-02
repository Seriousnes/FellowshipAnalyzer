using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

public sealed record AllyRuneUptime(int ActorId, bool IsTank, int ActiveMs, int PullDurationMs)
{
    public double Uptime => PullDurationMs > 0 ? Math.Min(1d, ActiveMs / (double)PullDurationMs) : 0;
}

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<Combatants>]
public sealed partial class RuneOfRenewalAnalyzer : Analyzer
{
    public int Casts { get; private set; }

    public IReadOnlyList<AllyRuneUptime> Allies => field ??= ComputeAllies();

    public double PartyUptime => Allies.Count == 0 ? 0 : Allies.Average(ally => ally.Uptime);

    public double? TankUptime => Allies.FirstOrDefault(ally => ally.IsTank)?.Uptime;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.RuneOfRenewal))]
    private void OnCast() => Casts++;

    private List<AllyRuneUptime> ComputeAllies()
    {
        var duration = Pull.EndTime - Pull.StartTime;
        if (duration <= 0) return [];

        var tanks = Owner.Actors
            .Where(actor => Hero.TryParse(actor.SubType, out var hero) && hero.Role == HeroRole.Tank)
            .Select(actor => actor.Id)
            .ToHashSet();

        return
        [
            .. PartyIds()
                .Select(id => new AllyRuneUptime(id, tanks.Contains(id), ActiveMsOn(id), duration))
                .OrderByDescending(ally => ally.IsTank)
                .ThenByDescending(ally => ally.ActiveMs)
                .ThenBy(ally => ally.ActorId)
        ];
    }

    private IEnumerable<int> PartyIds()
    {
        var party = Owner.Dungeon.FriendlyPlayers is { Count: > 0 } friendly ? friendly : [PlayerId];
        return party.Contains(PlayerId) ? party.Distinct() : party.Append(PlayerId).Distinct();
    }

    private int ActiveMsOn(int actorId)
    {
        var windows = Combatants.Units
            .Where(entry => entry.Key.ActorId == actorId)
            .SelectMany(entry => entry.Value.GetAuraWindows(Spells.RuneOfRenewalBuff, Pull.StartTime, Pull.EndTime, PlayerId))
            .ToList();

        return AuraWindowLedger.ActiveMs(windows);
    }
}
