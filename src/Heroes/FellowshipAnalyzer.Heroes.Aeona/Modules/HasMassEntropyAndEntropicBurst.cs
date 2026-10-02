using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Activation predicate for <see cref="EntropyClaimAnalyzer"/>: enables the analyzer when the
/// selected combatant has Mass Entropy equipped and Entropic Burst taken.
/// </summary>
public sealed class HasMassEntropyAndEntropicBurst : IModuleActivePredicate
{
    /// <summary><c>true</c> when the selected combatant has Mass Entropy equipped and Entropic Burst taken.</summary>
    public static bool IsActive(ParseContext context) =>
        context.SelectedCombatant.Legendary?.Id == Legendaries.MassEntropy.Id
        && context.SelectedCombatant.HasTalent(AeonaTalents.EntropicBurst);
}
