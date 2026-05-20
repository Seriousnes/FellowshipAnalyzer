using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules.Multishot;

internal class HasEmpoweredMultishot : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => context.SelectedCombatant.HasTalent(Talents.FocusedExpanse.Id);
}