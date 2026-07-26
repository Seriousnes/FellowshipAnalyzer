using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Measures how continuously Gunde kept Rend on a boss. Rend is the per-target accumulated bleed
/// most of Gunde's abilities feed, so a target carrying no Rend is a target taking a fraction of
/// his output. Windows are tracked per (TargetId, TargetInstance) from apply/refresh/remove events,
/// with a window still open at pull end closed at that target's last logged Rend event; the primary
/// target is the one carrying the most bleed time, so transient adds never dilute the boss reading.
/// Uptime is the primary target's covered time against the pull duration. Gaps are the uncovered
/// stretches between that target's windows; lead-in before the first application lowers uptime
/// without counting as a gap.
/// </summary>
/// <remarks>
/// Rend accumulates magnitude rather than a presented stack count, so presence windows are the
/// meaningful unit here. Its stack events are read purely as liveness: they run in the thousands per
/// pull, so a bleeding target that stops emitting them has left the fight.
/// </remarks>
[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class RendUptimeAnalyzer : DebuffUptimeAnalyzer, IRendAnalyzer
{
    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnApplied(ApplyDebuffEvent e) => OpenWindow(e, e.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnRefreshed(RefreshDebuffEvent e) => OpenWindow(e, e.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnRemoved(RemoveDebuffEvent e) => CloseWindow(e, e.Timestamp);

    [On<ApplyDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnStackGained(ApplyDebuffStackEvent e) => ObserveTarget(e, e.Timestamp);

    [On<RemoveDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnStackLost(RemoveDebuffStackEvent e) => ObserveTarget(e, e.Timestamp);
}
