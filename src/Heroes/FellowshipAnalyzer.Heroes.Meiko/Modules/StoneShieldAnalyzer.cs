using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

public interface IStoneShieldAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class StoneShieldAnalyzer : SelfBuffAnalyzer, IStoneShieldAnalyzer
{
    private readonly List<int> _casts = [];

    protected override Spell Buff => Spells.StoneShieldBuff;

    public IReadOnlyList<int> Casts => _casts;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart) => Seed(pullStart.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.StoneShieldAlt))]
    private void OnCast(CastEvent castEvent) => _casts.Add(castEvent.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.StoneShieldBuff))]
    private void OnApplied(ApplyBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.StoneShieldBuff))]
    private void OnStackGained(ApplyBuffStackEvent buffEvent) => SetStacks(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.StoneShieldBuff))]
    private void OnStackLost(RemoveBuffStackEvent buffEvent) => SetStacks(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.StoneShieldBuff))]
    private void OnRemoved(RemoveBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 0);
}
