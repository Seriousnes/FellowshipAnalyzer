using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declares an event handler on an <see cref="EventSubscriber"/>. The source generator emits
/// a <c>RegisterAttributeSubscriptions</c> override that wires every <see cref="OnAttribute{TEvent}"/>
/// directly into the <see cref="EventEmitter"/> with inlined predicates — no expression trees, no
/// runtime compilation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OnAttribute<TEvent> : Attribute
    where TEvent : Event
{
    /// <summary>Restrict to events whose source matches the selected actor(s).</summary>
    public Actor By { get; set; }

    /// <summary>Restrict to events whose target matches the selected actor(s).</summary>
    public Actor To { get; set; }

    /// <summary>Restrict to a single <see cref="IAbilityEvent"/> ability GUID.</summary>
    public int Spell { get; set; }

    /// <summary>Restrict to one of several <see cref="IAbilityEvent"/> ability GUIDs.</summary>
    public int[]? Spells { get; set; }

    /// <summary>Restrict to a single <see cref="IExtraAbilityEvent"/> ability GUID.</summary>
    public int ExtraSpell { get; set; }

    /// <summary>Restrict to one of several <see cref="IExtraAbilityEvent"/> ability GUIDs.</summary>
    public int[]? ExtraSpells { get; set; }
}
