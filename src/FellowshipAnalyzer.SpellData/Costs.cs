using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

public static class Costs
{
    private static readonly IReadOnlyDictionary<ResourceTypes, int> None = new Dictionary<ResourceTypes, int>();

    public static IReadOnlyDictionary<ResourceTypes, int> Map(
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
