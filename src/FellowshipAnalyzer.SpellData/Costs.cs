using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

public static class Costs
{
    private static readonly Dictionary<ResourceTypes, int> None = [];

    public static Dictionary<ResourceTypes, int> Map(
        ExportAbility ability, ICollection<Gap> gaps, string scope, string member)
    {
        if (ability.Cost is not { } cost || ability.Resource is not { } resource)
            return None;

        if (ability.CostIsFraction)
            return None;

        if (!ResourceTypesAliases.TryResolve(resource, out var slot))
        {
            gaps.Add(new Gap(scope, member, GapKind.UnknownResource));
            return None;
        }

        return new Dictionary<ResourceTypes, int> { [slot] = (int)Math.Round(cost) };
    }
}
