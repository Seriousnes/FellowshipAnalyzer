using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// One line of an <see cref="AuraChecklist"/>: an aura existed or was missing, optionally
/// carrying how many stacks or instances existed.
/// </summary>
/// <param name="Spell">The spell that names, icons and links the line. Pass the ability the player casts
/// rather than the aura it leaves, so the line reads the way the player thinks of it.</param>
/// <param name="Pass">Whether the aura existed.</param>
/// <param name="Count">The magnitude beside the name — instances, stacks, targets — or null when
/// presence is the whole story. Rendered only above one, so a single window never reads "x1".</param>
/// <param name="CountTooltip">What the count means, for example "3 concurrent applications".</param>
/// <param name="Note">A short trailing qualifier, for example how many enemies carried the aura.</param>
public sealed record AuraCheckItem(
    Spell Spell,
    bool Pass,
    int? Count = null,
    string? CountTooltip = null,
    string? Note = null);
