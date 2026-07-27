using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public sealed partial class RendStackTracker : EventSubscriber
{
    private readonly Dictionary<RendTarget, int> _stacks = [];
    private readonly List<RendRemoval> _removals = [];

    public int TotalStacks => _stacks.Values.Sum();

    public int BleedingTargets => _stacks.Count;

    public IReadOnlyList<RendRemoval> Removals => _removals;

    public int StacksOn(int targetId, int? targetInstance) =>
        _stacks.GetValueOrDefault(new RendTarget(targetId, targetInstance ?? 0));

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnApplied(ApplyDebuffEvent debuffEvent) => _stacks[Key(debuffEvent)] = 1;

    [On<ApplyDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnStackGained(ApplyDebuffStackEvent debuffEvent) => _stacks[Key(debuffEvent)] = debuffEvent.Stack;

    [On<RemoveDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnStackLost(RemoveDebuffStackEvent debuffEvent) => _stacks[Key(debuffEvent)] = debuffEvent.Stack;

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnRemoved(RemoveDebuffEvent debuffEvent)
    {
        var target = Key(debuffEvent);
        if (!_stacks.Remove(target, out var stacks) || stacks <= 0) return;

        _removals.Add(new RendRemoval(debuffEvent.Timestamp, target, stacks));
    }

    private static RendTarget Key(IHasTargetWithInstanceEvent debuffEvent) =>
        new(debuffEvent.TargetId, debuffEvent.TargetInstance ?? 0);
}

public readonly record struct RendTarget(int TargetId, int TargetInstance);

public readonly record struct RendRemoval(int Timestamp, RendTarget Target, int Stacks);
