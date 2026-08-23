using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Base type for the apply, refresh, remove, and stack-change events raised for buffs and debuffs.</summary>
public abstract class BuffEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent, IHasSourceWithInstanceEvent
{
    /// <summary>The ability associated with this buff or debuff.</summary>
    public virtual Ability Ability { get; set; } = new();
    /// <summary>The FSLID identifying <see cref="Ability"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>Disambiguates the target when multiple copies of the same enemy share <see cref="TargetId"/>.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>The unique id of the actor the buff or debuff is applied to.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Disambiguates the source when multiple copies of the same enemy share <see cref="SourceId"/>.</summary>
    public virtual int? SourceInstance { get; set; }
    /// <summary>The unique id of the actor that applied, refreshed, or removed the buff or debuff.</summary>
    public virtual int SourceId { get; set; }
}

/// <summary>A single continuous application window of a buff or debuff, fabricated by <see cref="Analysis.Combatants"/> from the underlying apply, refresh, remove, and stack events.</summary>
[Fabricated]
public class TrackedBuffEvent : BuffEvent
{
    /// <summary>The timestamp, in milliseconds, at which this window started.</summary>
    public int Start { get; set; }
    /// <summary>The timestamp, in milliseconds, at which this window ended, or <c>null</c> while the buff or debuff is still active.</summary>
    public int? End { get; set; }
    /// <summary>Every stack-count change recorded during this window, in chronological order.</summary>
    public List<StackHistoryElement> StackHistory { get; set; } = [];
    /// <summary>The timestamp of every refresh recorded during this window, where a refresh reapplies the buff or debuff without changing its stack count.</summary>
    public List<int> RefreshHistory { get; set; } = [];
    /// <summary>The current stack count of this buff or debuff.</summary>
    public int Stacks { get; set; }
    /// <summary>Whether this window tracks a debuff rather than a buff.</summary>
    public bool IsDebuff { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;

    /// <summary>One entry in <see cref="StackHistory"/>, pairing a stack count with the timestamp it was recorded at.</summary>
    public class StackHistoryElement
    {
        /// <summary>The stack count at <see cref="Timestamp"/>.</summary>
        public int Stacks { get; set; }
        /// <summary>The timestamp, in milliseconds, at which <see cref="Stacks"/> was recorded.</summary>
        public int Timestamp { get; set; }
    }
}

#region Buffs

/// <summary>Raised when a buff is first applied to a unit.</summary>
public class ApplyBuffEvent : BuffEvent
{
    /// <summary>The absorb shield amount granted by this buff application, if any.</summary>
    public virtual int? Absorb { get; set; }
    /// <summary>Whether FellowshipLogs synthesized this application from the actor's combatant-info snapshot because the buff was already active when logging began, rather than observing it live.</summary>
    public virtual bool? FromCombatantInfo { get; set; }
}

/// <summary>Raised when an additional stack of a buff is applied.</summary>
public class ApplyBuffStackEvent : BuffEvent, IBuffStackEvent
{
    /// <summary>The stack count reported by this application.</summary>
    public virtual int Stack { get; set; }
}

/// <summary>Raised when a buff expires or is removed entirely.</summary>
public class RemoveBuffEvent : BuffEvent
{
    /// <summary>The absorb shield amount that remained when this buff was removed, if any.</summary>
    public virtual int? Absorb { get; set; }
}

/// <summary>Raised when a single stack of a buff is removed.</summary>
public class RemoveBuffStackEvent : BuffEvent, IBuffStackEvent
{
    /// <summary>The stack count reported by this removal.</summary>
    public virtual int Stack { get; set; }
}

/// <summary>Raised when a buff's duration is refreshed without changing its stack count.</summary>
public class RefreshBuffEvent : BuffEvent
{
    /// <summary>The absorb shield amount active on the target when this buff was refreshed, if any.</summary>
    public virtual int? Absorb { get; set; }
    /// <summary>The unit that reapplied the buff.</summary>
    public virtual ICastTarget? Source { get; set; }
}

#endregion


#region Debuffs

/// <summary>Raised when a debuff is first applied to a unit.</summary>
public class ApplyDebuffEvent : BuffEvent
{
    /// <summary>The unit that applied the debuff, defaulting to <see cref="DefaultActors.Environment"/> when the source is not a report actor.</summary>
    public virtual Unit? Source { get; set; } = DefaultActors.Environment;
    /// <summary>The absorb shield amount granted by this debuff application, if any.</summary>
    public virtual int? Absorb { get; set; }
    /// <summary>Whether FellowshipLogs synthesized this application from the actor's combatant-info snapshot because the debuff was already active when logging began, rather than observing it live.</summary>
    public virtual bool? FromCombatantInfo { get; set; }
}

/// <summary>Raised when an additional stack of a debuff is applied.</summary>
public class ApplyDebuffStackEvent : BuffEvent, IBuffStackEvent
{
    /// <summary>The stack count reported by this application.</summary>
    public virtual int Stack { get; set; }
}

/// <summary>Raised when a debuff expires or is removed entirely.</summary>
public class RemoveDebuffEvent : BuffEvent
{
    /// <summary>The unit that removed the debuff, defaulting to <see cref="DefaultActors.Environment"/> when the source is not a report actor.</summary>
    public virtual Unit? Source { get; set; } = DefaultActors.Environment;
    /// <summary>The absorb shield amount that remained when this debuff was removed, if any.</summary>
    public virtual int? Absorb { get; set; }
}

/// <summary>Raised when a single stack of a debuff is removed.</summary>
public class RemoveDebuffStackEvent : BuffEvent, IBuffStackEvent
{
    /// <summary>The stack count reported by this removal.</summary>
    public virtual int Stack { get; set; }
}

/// <summary>Raised when a debuff's duration is refreshed without changing its stack count.</summary>
public class RefreshDebuffEvent : BuffEvent
{
    /// <summary>The unit that reapplied the debuff.</summary>
    public virtual ICastTarget? Source { get; set; }
}

#endregion
