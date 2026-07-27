using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Implemented by an event whose payload names the ability responsible for it.</summary>
public interface IAbilityEvent
{
    /// <summary>The ability resolved for this event from report master data during normalization.</summary>
    Ability Ability { get; set; }
    /// <summary>The raw <see cref="FSLID"/> the log reported for the ability, before <see cref="Ability"/> is resolved.</summary>
    FSLID AbilityGameId { get; set; }
}

/// <summary>Implemented by an event that carries a second ability alongside its primary <see cref="IAbilityEvent.Ability"/>, such as the ability an interrupt or dispel consumed.</summary>
public interface IExtraAbilityEvent
{
    /// <summary>The secondary ability resolved for this event from report master data during normalization.</summary>
    Ability? ExtraAbility { get; set; }
    /// <summary>The raw <see cref="FSLID"/> the log reported for the secondary ability, before <see cref="ExtraAbility"/> is resolved.</summary>
    FSLID ExtraAbilityGameId { get; set; }
}

/// <summary>Implemented by an event whose payload carries a numeric magnitude, such as damage or healing dealt.</summary>
public interface IAmountEvent
{
    /// <summary>The magnitude this event reports, in whatever unit its concrete event type defines.</summary>
    long Amount { get; set; }
}

/// <summary>Implemented by an event that names the actor who caused it.</summary>
public interface IHasSourceEvent
{
    /// <summary>The actor id of the unit that caused this event.</summary>
    int SourceId { get; set; }
}

/// <summary>Implemented by an event whose source actor may be one of several concurrent spawns sharing the same actor id.</summary>
public interface IHasSourceWithInstanceEvent : IHasSourceEvent
{
    /// <summary>Distinguishes which spawn of <see cref="IHasSourceEvent.SourceId"/> caused this event, when more than one is alive at once.</summary>
    int? SourceInstance { get; set; }
}

/// <summary>Implemented by an event that names the actor it affected.</summary>
public interface IHasTargetEvent
{
    /// <summary>The actor id of the unit this event affected.</summary>
    int TargetId { get; set; }
}

/// <summary>Implemented by an event whose target actor may be one of several concurrent spawns sharing the same actor id.</summary>
public interface IHasTargetWithInstanceEvent : IHasTargetEvent
{
    /// <summary>Distinguishes which spawn of <see cref="IHasTargetEvent.TargetId"/> this event affected, when more than one is alive at once.</summary>
    int? TargetInstance { get; set; }
}

/// <summary>The raw unit info the log embeds for a cast or buff's source or target, ahead of any resolution against tracked combatants.</summary>
public interface ICastTarget
{
    /// <summary>The unit's display name as reported by the log.</summary>
    string Name { get; set; }
    /// <summary>The actor id of the unit.</summary>
    int Id { get; set; }
    /// <summary>The game id identifying the unit's underlying NPC or player record.</summary>
    int Guid { get; set; }
    /// <summary>The unit type as a raw string from the log, for example <c>"player"</c> or <c>"npc"</c>.</summary>
    string Type { get; set; }
    /// <summary>The icon asset name associated with the unit.</summary>
    string Icon { get; set; }
}

/// <summary>Implemented by an event that reports the stack count of a buff or debuff after it fired.</summary>
public interface IBuffStackEvent
{
    /// <summary>The stack count as reported by the log at the time of this event.</summary>
    int Stack { get; set; }
}

/// <summary>Marks an event type that can be the cast which started a cooldown, so it can populate a cooldown event's <c>Trigger</c> property.</summary>
public interface ICooldownTriggerEvent
{
}

/// <summary>Non-generic marker so a <see cref="PhaseConfig.Filter"/> can hold an <see cref="IPhaseFilter{T}"/> without knowing its event type at compile time.</summary>
public interface IPhaseFilter { }
/// <summary>Restricts a <see cref="PhaseConfig"/> transition to events of a specific type.</summary>
/// <typeparam name="T">The event type this filter matches against.</typeparam>
public interface IPhaseFilter<T> : IPhaseFilter where T : Event
{
    /// <summary>The event criteria this filter matches against.</summary>
    T Type { get; set; }
}