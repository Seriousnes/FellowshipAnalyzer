using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declares an event handler on an <see cref="Analyzer"/>. The source generator emits
/// a <c>RegisterAttributeSubscriptions</c> override that wires every <see cref="OnAttribute{TEvent}"/>
/// directly into the <see cref="EventEmitter"/> with inlined predicates.
/// <para>
/// The handler takes the dispatched event as its single parameter, typed as <typeparamref name="TEvent"/>,
/// one of its base classes or interfaces, or a <c>OneOf&lt;…&gt;</c> carrying a slot for it. Declare no
/// parameter at all when the handler reads nothing off the event: the generator then emits a call with
/// no argument, and the attribute's own <see cref="By"/>, <see cref="To"/> and spell filters still apply.
/// </para>
/// <para>
/// <see cref="Spell"/>, <see cref="Spells"/>, <see cref="ExtraSpell"/>, and <see cref="ExtraSpells"/>
/// take <c>nameof(Registry.Member)</c> expressions referencing a static <see cref="Common.Spells.Spell"/>
/// (or <see cref="Common.Spells.Effect"/>) property on a type implementing
/// <see cref="Common.Spells.ISpellRegistry"/>. The generator resolves the member at codegen time
/// and emits an <c>Ability.Id == &lt;fslid&gt;</c> predicate against the resolved
/// <see cref="Common.Spells.Spell.FSLID"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OnAttribute<TEvent> : Attribute
    where TEvent : Event
{
    /// <summary>Restrict to events whose source matches the selected actor(s).</summary>
    public Actor By { get; set; }

    /// <summary>Restrict to events whose target matches the selected actor(s).</summary>
    public Actor To { get; set; }

    /// <summary>
    /// Restrict to a single <see cref="IAbilityEvent"/>. Pass <c>nameof(Registry.Member)</c>;
    /// the generator resolves the member's <see cref="Common.Spells.Spell.FSLID"/>.
    /// </summary>
    public string? Spell { get; set; }

    /// <summary>
    /// Restrict to one of several <see cref="IAbilityEvent"/> abilities. Each element must be
    /// <c>nameof(Registry.Member)</c>.
    /// </summary>
    public string[]? Spells { get; set; }

    /// <summary>
    /// Restrict to a single <see cref="IExtraAbilityEvent"/>. Pass <c>nameof(Registry.Member)</c>;
    /// the generator resolves the member's <see cref="Common.Spells.Spell.FSLID"/>.
    /// </summary>
    public string? ExtraSpell { get; set; }

    /// <summary>
    /// Restrict to one of several <see cref="IExtraAbilityEvent"/> abilities. Each element must be
    /// <c>nameof(Registry.Member)</c>.
    /// </summary>
    public string[]? ExtraSpells { get; set; }
}
