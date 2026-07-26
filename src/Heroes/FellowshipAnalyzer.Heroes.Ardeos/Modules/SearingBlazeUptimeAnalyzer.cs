using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures how continuously Ardeos kept the Searing Blaze DoT rolling on a boss pull. Windows are
/// tracked per (TargetId, TargetInstance) from apply/refresh/remove events, with a window still open
/// at pull end closed at that target's last logged DoT event; the primary target is the one carrying
/// the most DoT time, so transient adds never dilute the boss reading. Uptime is the primary
/// target's covered time against the pull duration. Gaps are the uncovered stretches between that
/// target's windows; lead-in before the first application lowers uptime without counting as a gap.
/// </summary>
/// <remarks>
/// The DoT's own damage ticks are its liveness signal: a target still burning keeps logging them, so
/// one that stops has left the fight and its window ends there.
/// </remarks>
[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class SearingBlazeUptimeAnalyzer : DebuffUptimeAnalyzer, ISearingBlazeAnalyzer
{
    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnApplied(ApplyDebuffEvent e) => OpenWindow(e, e.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRefreshed(RefreshDebuffEvent e) => OpenWindow(e, e.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRemoved(RemoveDebuffEvent e) => CloseWindow(e, e.Timestamp);

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnTicked(DamageEvent e) => ObserveTarget(e, e.Timestamp);
}
