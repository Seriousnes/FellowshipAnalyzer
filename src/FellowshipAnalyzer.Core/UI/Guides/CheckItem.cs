using Microsoft.AspNetCore.Components;

using OneOf;

using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// What names one line of a <see cref="Checklist"/>: a spell, which renders as a linked icon and
/// name, plain text, or arbitrary markup.
/// </summary>
[GenerateOneOf]
public partial class CheckLabel : OneOfBase<Spell, string, RenderFragment>;

/// <summary>
/// One line of a <see cref="Checklist"/>: something was there or it was missing, optionally with
/// how many of it there were.
/// </summary>
public sealed record CheckItem
{
    /// <summary>
    /// What names the line. Pass the ability the player casts rather than the aura it leaves, where
    /// the two differ and the cast is how the player thinks of it.
    /// </summary>
    public required CheckLabel Label { get; init; }

    /// <summary>Whether the line passed.</summary>
    public required bool Pass { get; init; }

    /// <summary>
    /// The magnitude beside the name - instances, stacks, targets - or <c>null</c> when presence is
    /// the whole story. Rendered only above one, so a single window never reads "x1".
    /// </summary>
    public int? Count { get; init; }

    /// <summary>What the count means, for example "3 concurrent applications".</summary>
    public string? CountTooltip { get; init; }

    /// <summary>A short trailing qualifier, for example how many enemies had the aura active.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// The tooltip on the pass or fail mark, for example "Active at the cast". Defaults to "Pass" and
    /// "Fail" when left null.
    /// </summary>
    public string? Title { get; init; }
}
