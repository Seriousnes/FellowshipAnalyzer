using System.Text.Json.Serialization;

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
    /// <summary>The <see cref="Duration"/> value, in milliseconds, the log states for an application with no end.</summary>
    public const int InfiniteDurationMs = 9_999_000;
    /// <summary>The length of this application in milliseconds, as the log states it on an apply or refresh. <c>null</c> on an event the log gives no duration.</summary>
    public virtual int? Duration { get; set; }
    /// <summary>The timestamp at which this application ends when nothing removes or refreshes it. <c>null</c> when <see cref="Duration"/> is absent or <see cref="InfiniteDurationMs"/>.</summary>
    [JsonIgnore]
    public virtual int? ExpectedEnd => Duration is { } duration && duration < InfiniteDurationMs ? Timestamp + duration : null;
}

/// <summary>A single continuous application window of a buff or debuff, fabricated by <see cref="Analysis.Combatants"/> from the underlying apply, refresh, remove, and stack events.</summary>
[Fabricated]
public class TrackedBuffEvent : BuffEvent
{
    /// <summary>The timestamp, in milliseconds, at which this window started.</summary>
    public int Start { get; set; }
    /// <summary>The timestamp, in milliseconds, at which this window ended, or <c>null</c> while the buff or debuff is still active.</summary>
    public int? End { get; set; }
    /// <summary><c>true</c> when <see cref="End"/> was set by the end of the dungeon, because the log states no removal of this window.</summary>
    public bool EndedByDungeonEnd { get; set; }
    private int? _expectedEnd;
    /// <summary>The timestamp at which this window ends when no removal arrives: the <see cref="BuffEvent.ExpectedEnd"/> of its latest apply or refresh that states a duration. <c>null</c> when that duration is absent or <see cref="BuffEvent.InfiniteDurationMs"/>.</summary>
    [JsonIgnore]
    public override int? ExpectedEnd => _expectedEnd;
    /// <summary>Takes <see cref="BuffEvent.Duration"/> and <see cref="ExpectedEnd"/> from an apply or refresh event. An event with no logged duration leaves a window's expected end unchanged.</summary>
    internal void ExpectFrom(BuffEvent logged)
    {
        if (logged.Duration is null)
            return;

        Duration = logged.Duration;
        _expectedEnd = logged.ExpectedEnd;
    }
    /// <summary>Every stack-count change during this window, in chronological order.</summary>
    public List<StackHistoryElement> StackHistory { get; set; } = [];
    /// <summary>The timestamp of every refresh during this window, where a refresh reapplies the buff or debuff without changing its stack count.</summary>
    public List<int> RefreshHistory { get; set; } = [];
    /// <summary>The current stack count of this buff or debuff.</summary>
    public int Stacks { get; set; }
    /// <summary>Whether this window tracks a debuff rather than a buff.</summary>
    public bool IsDebuff { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;

    /// <summary>One entry in <see cref="StackHistory"/>, pairing a stack count with its timestamp.</summary>
    public class StackHistoryElement
    {
        /// <summary>The stack count at <see cref="Timestamp"/>.</summary>
        public int Stacks { get; set; }
        /// <summary>The timestamp, in milliseconds, of <see cref="Stacks"/>.</summary>
        public int Timestamp { get; set; }
    }
}

#region Buffs

/// <summary>Raised when a buff is first applied to a unit.</summary>
public class ApplyBuffEvent : BuffEvent
{
    /// <summary>The absorb shield amount granted by this buff application, if any.</summary>
    public virtual int? Absorb { get; set; }
    /// <summary>Whether FellowshipLogs synthesized this application from the actor's combatant-info because the buff was already active when logging began.</summary>
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
    /// <summary>Whether FellowshipLogs synthesized this application from the actor's combatant-info because the debuff was already active when logging began.</summary>
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
