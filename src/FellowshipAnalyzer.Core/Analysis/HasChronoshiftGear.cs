using FellowshipAnalyzer.Core.Common.Items;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Activation predicate for <see cref="ChronoshiftAnalyzer"/>: enables the module when the
/// selected combatant has any weapon that grants the Chronoshift ability equipped.
/// </summary>
public sealed class HasChronoshiftGear : IModuleActivePredicate
{
    /// <summary><c>true</c> when the selected combatant has any weapon that grants Chronoshift equipped.</summary>
    public static bool IsActive(ParseContext context) =>
        Items.ChronoshiftGrantingItemIds.Any(context.SelectedCombatant.HasGear);
}
