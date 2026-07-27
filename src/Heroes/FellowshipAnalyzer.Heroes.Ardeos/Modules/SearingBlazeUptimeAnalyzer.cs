using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

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
