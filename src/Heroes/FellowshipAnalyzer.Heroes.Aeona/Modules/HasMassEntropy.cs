using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Activation predicate for <see cref="EntropyClaimAnalyzer"/>: enables the analyzer when the
/// selected combatant has Mass Entropy equipped.
/// </summary>
public sealed class HasMassEntropy : IModuleActivePredicate
{
    /// <summary><c>true</c> when the selected combatant has Mass Entropy equipped.</summary>
    public static bool IsActive(ParseContext context) => context.SelectedCombatant.Legendary?.Id == Legendaries.MassEntropy.Id;
}
