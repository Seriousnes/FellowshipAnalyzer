using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declares an event handler on an <see cref="EventSubscriber"/>. The source generator emits
/// a <c>RegisterAttributeSubscriptions</c> override that wires every <see cref="OnAttribute{TEvent}"/>
/// directly into the <see cref="EventEmitter"/> with inlined predicates.
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
