using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

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
