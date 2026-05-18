using FellowshipAnalyzer.Core.Common.Items;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Activation predicate for <see cref="ChronoshiftAnalyzer"/>: enables the module only when
/// the selected combatant has Asha's Chronoshift Spire equipped.
/// </summary>
public sealed class HasChronoshiftGear : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) =>
        context.SelectedCombatant?.HasGear(Items.AshasChronoshiftSpire.Id) ?? false;
}
