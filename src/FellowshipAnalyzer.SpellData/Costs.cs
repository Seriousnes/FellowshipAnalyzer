using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// Maps a merged scalar dictionary and a per-hero <see cref="ResourceModel"/> to resource-keyed integer costs.
/// </summary>
public static class Costs
{
    /// <summary>
    /// Returns resource-keyed integer costs extracted from <paramref name="scalars"/>.
    /// <c>OrbCost</c> maps to <c>winterOrb</c>; <c>SpiritPointCost</c> maps to <c>spirit</c>;
    /// a generic <c>Cost</c> field resolves through <paramref name="resources"/> when
    /// <paramref name="costType"/> is the corresponding resource key string from game data.
    /// Only resources with a present cost are included; if <paramref name="costType"/> is
    /// absent or not in <paramref name="resources"/>, the generic cost is silently skipped.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Map(
        IReadOnlyDictionary<string, double> scalars,
        ResourceModel resources,
        string? costType = null)
    {
        var result = new Dictionary<string, int>();

        if (scalars.TryGetValue("OrbCost", out var orbCost))
            result["winterOrb"] = (int)Math.Round(orbCost);

        if (scalars.TryGetValue("SpiritPointCost", out var spiritCost))
            result["spirit"] = (int)Math.Round(spiritCost);

        if (costType is not null
            && scalars.TryGetValue("Cost", out var cost)
            && resources.CostTypeToResource.TryGetValue(costType, out var resourceKey))
            result[resourceKey] = (int)Math.Round(cost);

        return result;
    }
}
