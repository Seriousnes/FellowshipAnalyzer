using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>Reduces Repository of Frozen Light's cooldown by 0.3 seconds for each stack on a Frozen Light buff removed at or after 30 seconds from its application.</summary>
[ActiveWhen<HasRepositoryOfFrozenLightGear>]
[After<SpellUsable>]
public sealed partial class RepositoryOfFrozenLightAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    private const int BuffDurationMs = 30_000;

    private const int ReductionPerStackMs = 300;

    private readonly Dictionary<int, FrozenLightApplication> _applications = [];

    /// <summary>Requested and applied cooldown progress the Frozen Light stacks took off Repository of Frozen Light.</summary>
    public CooldownReductionResult CooldownReduction { get; private set; }

    [On<ApplyBuffEvent>(By = Actor.Player)]
    private void OnApply(ApplyBuffEvent buffEvent)
    {
        if (IsFrozenLight(buffEvent))
            _applications[buffEvent.TargetId] = new FrozenLightApplication(buffEvent.Timestamp, 1);
    }

    [On<ApplyBuffStackEvent>(By = Actor.Player)]
    private void OnApplyStack(ApplyBuffStackEvent buffEvent) => SetStacks(buffEvent, buffEvent.Stack);

    [On<RemoveBuffStackEvent>(By = Actor.Player)]
    private void OnRemoveStack(RemoveBuffStackEvent buffEvent) => SetStacks(buffEvent, buffEvent.Stack);

    [On<RemoveBuffEvent>(By = Actor.Player)]
    private void OnRemove(RemoveBuffEvent buffEvent)
    {
        if (!IsFrozenLight(buffEvent) || !_applications.Remove(buffEvent.TargetId, out var application)) return;
        if (buffEvent.Timestamp - application.AppliedAt < BuffDurationMs) return;

        CooldownReduction += _spellUsable.ReduceCooldown(
            Items.RepositoryOfFrozenLight.FSLID, (buffEvent.Stack ?? application.Stacks) * ReductionPerStackMs, buffEvent.Timestamp);
    }

    private void SetStacks(BuffEvent buffEvent, int stacks)
    {
        if (IsFrozenLight(buffEvent) && _applications.TryGetValue(buffEvent.TargetId, out var application))
            _applications[buffEvent.TargetId] = application with { Stacks = stacks };
    }

    private static bool IsFrozenLight(BuffEvent buffEvent) =>
        buffEvent.Ability.FSLID == Items.RepositoryOfFrozenLightBuff.FSLID;

    private readonly record struct FrozenLightApplication(int AppliedAt, int Stacks);
}
