using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Meiko;
using FellowshipAnalyzer.Core.Events;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

public interface ISpiritedStrikesAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SpiritedStrikesAnalyzer : SelfBuffAnalyzer, ISpiritedStrikesAnalyzer
{
    protected override Spell Buff => Spells.SpiritedStrikes;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart) => Seed(pullStart.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SpiritedStrikes))]
    private void OnApplied(ApplyBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 1);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SpiritedStrikes))]
    private void OnRefreshed(RefreshBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 1);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SpiritedStrikes))]
    private void OnRemoved(RemoveBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 0);
}
