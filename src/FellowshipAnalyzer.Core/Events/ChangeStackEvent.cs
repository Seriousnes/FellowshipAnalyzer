namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Fabricated by <see cref="Analysis.Combatants"/> whenever a tracked buff or
/// debuff's stack count changes, carrying the before and after stack counts for the aura instance.
/// </summary>
[Fabricated]
public abstract class ChangeStackEvent : BuffEvent
{
    /// <summary>A single stack-history entry: the stack count reached at a given point in time.</summary>
    /// <param name="Stacks">The stack count reached at this point in the aura's history.</param>
    /// <param name="Timestamp">The dungeon-relative timestamp, in milliseconds, at which this stack count was reached.</param>
    public record class History(int Stacks, long Timestamp);

    /// <summary>The dungeon-relative timestamp, in milliseconds, at which the tracked aura ended, or <c>null</c> while it is still active.</summary>
    public virtual int? End { get; set; }
    /// <summary>Whether the aura whose stack count changed is a debuff rather than a buff.</summary>
    public virtual bool? IsDebuff { get; set; }
    /// <summary>The aura's stack count immediately after this change.</summary>
    public virtual int NewStacks { get; set; }
    /// <summary>The aura's stack count immediately before this change.</summary>
    public virtual int OldStacks { get; set; }
    /// <summary>The stack count reported directly on the triggering event; not populated by the built-in stack-change fabricator, which sets <see cref="NewStacks"/> and <see cref="Stacks"/> instead.</summary>
    public virtual int? Stack { get; set; }
    /// <summary>The <see cref="History"/> entries recorded for this change.</summary>
    public virtual List<History> StackHistory { get; set; } = [];
    /// <summary>The aura's stack count after this change, matching <see cref="NewStacks"/>.</summary>
    public virtual int Stacks { get; set; }
    /// <summary>The signed change in stack count for this event; negative when stacks were lost.</summary>
    public virtual int StacksGained { get; set; }
    /// <summary>The dungeon-relative timestamp, in milliseconds, at which the tracked aura was first applied.</summary>
    public virtual int Start { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}

/// <summary>Fabricated whenever a buff's stack count changes.</summary>
public class ChangeBuffStackEvent : ChangeStackEvent { }

/// <summary>Fabricated whenever a debuff's stack count changes.</summary>
public class ChangeDebuffStackEvent : ChangeStackEvent { }