namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Activation predicate for <see cref="TheMonarchAnalyzer"/>: enables the module when the selected combatant
/// has The Monarch slotted into any equipped item.
/// </summary>
public sealed class HasTheMonarch : IModuleActivePredicate
{
    /// <summary><c>true</c> when the selected combatant has The Monarch slotted.</summary>
    public static bool IsActive(ParseContext context) =>
        context.SelectedCombatant.BlessingLevel(TheMonarchAnalyzer.Blessing) > 0;
}
