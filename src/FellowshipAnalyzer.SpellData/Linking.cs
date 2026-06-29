using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>A resolved link between a hero ability and one of the effects it spawns.</summary>
/// <param name="AbilityFslId">FSL id of the Kit ability.</param>
/// <param name="EffectFslId">FSL id of the matched effect.</param>
/// <param name="Role">Full tail of the effect DevName after the <c>GE_{Class}_{core}_</c> stem (e.g. <c>Damage</c>, <c>Debuff</c>, <c>Talent_BuffWhileActive</c>).</param>
public record EffectLink(int AbilityFslId, int EffectFslId, string Role);

/// <summary>DevName-based linking between hero Kit abilities, their spawned effects, and their Constants entries.</summary>
public static class Linking
{
    /// <summary>
    /// Splits a DevName into its core stem and optional trailing role.
    /// For <c>GA_{Class}_{core}</c> abilities the role is <see langword="null"/>.
    /// For <c>GE_{Class}_{core}_{Role}</c> effects the role is the last underscore-delimited segment.
    /// </summary>
    public static (string core, string? role) SplitDevName(string devName)
    {
        if (devName.StartsWith("GE_", StringComparison.Ordinal))
        {
            var body = devName["GE_".Length..];
            var lastUnderscore = body.LastIndexOf('_');
            return lastUnderscore >= 0
                ? (body[..lastUnderscore], body[(lastUnderscore + 1)..])
                : (body, null);
        }

        if (devName.StartsWith("GA_", StringComparison.Ordinal))
            return (devName["GA_".Length..], null);

        return (devName, null);
    }

    /// <summary>
    /// Returns every <see cref="ConstantsEntry"/> in <paramref name="hero"/> whose <c>DevName</c>
    /// exactly matches the Kit ability's <c>DevName</c>.
    /// </summary>
    public static IReadOnlyList<ConstantsEntry> ConstantsFor(KitAbility kit, HeroRecord hero) =>
        hero.Constants.Where(c => c.DevName == kit.DevName).ToList();

    /// <summary>
    /// For each Kit ability whose DevName starts with <c>GA_{hero.DevKey}_</c>, finds every effect in
    /// <paramref name="spells"/> whose DevName starts with the corresponding <c>GE_{hero.DevKey}_{core}_</c>
    /// stem and returns an <see cref="EffectLink"/> with the full tail as the role.
    /// </summary>
    public static IReadOnlyList<EffectLink> LinkEffects(HeroRecord hero, SpellDataSource spells)
    {
        var prefix = $"GA_{hero.DevKey}_";
        var links = new List<EffectLink>();

        foreach (var kit in hero.Kit)
        {
            if (!kit.DevName.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var stem = "GE_" + kit.DevName["GA_".Length..];
            var stemWithSeparator = stem + "_";

            foreach (var effect in spells.Effects.Values)
            {
                if (!effect.DevName.StartsWith(stemWithSeparator, StringComparison.Ordinal))
                    continue;

                var role = effect.DevName[stemWithSeparator.Length..];
                links.Add(new EffectLink(kit.FslId, effect.FslId, role));
            }
        }

        return links;
    }
}
