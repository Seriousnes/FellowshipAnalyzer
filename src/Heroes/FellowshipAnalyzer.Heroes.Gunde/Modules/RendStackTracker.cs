using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Keeps a live count of the Rend standing on every enemy Gunde has bled. Rend is the accumulated
/// bleed the rest of the kit feeds and two abilities cash out - Heart Splitter exsanguinates a share
/// of it without consuming it, Slaughter consumes all of it - so the size of the pile at the instant
/// one of those is pressed is the thing worth measuring. It is a stack count rather than a damage
/// figure, so it reads the same on any gear.
/// </summary>
/// <remarks>
/// <para>
/// The count is read straight from the Rend debuff stream, keyed by (TargetId, TargetInstance), the
/// unit the game applies a debuff to. <see cref="ApplyDebuffStackEvent.Stack"/> and
/// <see cref="RemoveDebuffStackEvent.Stack"/> both carry an absolute count, verified against live
/// Season 3 data where a single enemy's Rend climbs past 300 in fine-grained steps. A fresh
/// application starts at one and the stack event that follows corrects it.
/// </para>
/// <para>
/// Every removal is recorded in <see cref="Removals"/> with the count that was standing, because the
/// removal event itself carries none. Slaughter's consumption is a burst of removals sharing the
/// cast's millisecond, so an analyzer attributes them by proximity to its own cast rather than this
/// tracker guessing at a cause; a bleed that simply expired is a removal too.
/// </para>
/// <para>
/// This runs for the whole fight so pull analyzers can read a pile that was built before their pull
/// opened. Consumers filter <see cref="Removals"/> by timestamp, which confines them to their own
/// pull without the tracker needing a pull lifetime.
/// </para>
/// </remarks>
public sealed partial class RendStackTracker : EventSubscriber
{
    private readonly Dictionary<RendTarget, int> _stacks = [];
    private readonly List<RendRemoval> _removals = [];

    /// <summary>Rend standing across every enemy currently bleeding.</summary>
    public int TotalStacks => _stacks.Values.Sum();

    /// <summary>Enemies currently carrying Rend.</summary>
    public int BleedingTargets => _stacks.Count;

    /// <summary>Every Rend removal observed, in encounter order, with the count it took away.</summary>
    public IReadOnlyList<RendRemoval> Removals => _removals;

    /// <summary>Rend standing on one enemy right now, or zero when it carries none.</summary>
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

/// <summary>One bleeding enemy, identified the way the game applies a debuff.</summary>
public readonly record struct RendTarget(int TargetId, int TargetInstance);

/// <summary>Rend leaving one enemy, and how much of it was standing when it went.</summary>
/// <param name="Timestamp">Encounter time of the removal.</param>
/// <param name="Target">The enemy the bleed left.</param>
/// <param name="Stacks">Rend that was standing, which the removal event itself does not carry.</param>
public readonly record struct RendRemoval(int Timestamp, RendTarget Target, int Stacks);
