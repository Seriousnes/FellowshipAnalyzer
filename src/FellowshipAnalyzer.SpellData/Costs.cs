using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// Maps a merged scalar dictionary and a per-hero <see cref="ResourceModel"/> to
/// <see cref="ResourceTypes"/>-keyed integer costs.
/// </summary>
public static class Costs
{
    public static IReadOnlyDictionary<ResourceTypes, int> Map(
        IReadOnlyDictionary<string, double> scalars,
        ResourceModel resources,
        string? costType = null)
    {
        var result = new Dictionary<ResourceTypes, int>();

        if (scalars.TryGetValue("OrbCost", out var orbCost))
            result[ResourceTypes.Tertiary] = (int)Math.Round(orbCost);

        if (scalars.TryGetValue("SpiritPointCost", out var spiritCost))
            result[ResourceTypes.Spirit] = (int)Math.Round(spiritCost);

        if (costType is not null
            && scalars.TryGetValue("Cost", out var cost)
            && resources.CostTypeToResource.TryGetValue(costType, out var slot))
            result[slot] = (int)Math.Round(cost);

        return result;
    }
}
