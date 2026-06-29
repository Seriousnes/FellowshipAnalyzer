using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// All upstream data sources loaded and ready for <see cref="MergeEngine.Run"/>.
/// Declared as a record so Task 9 can produce patched variants via <c>with { … }</c>.
/// </summary>
public record MergeInputs(
    SpellDataSource Spells,
    GearDataSource Gear,
    HeroDataSource Heroes,
    IconSource Icons,
    OverridesSource Overrides,
    DevNameMappings Names)
{
    /// <summary>Loads every upstream source from the committed file paths.</summary>
    public static MergeInputs Load() => new(
        SpellDataSource.Load(SourcePaths.SpellData),
        GearDataSource.Load(SourcePaths.GearData),
        HeroDataSource.Load(SourcePaths.HeroData),
        IconSource.Load(SourcePaths.Abilities),
        OverridesSource.Load(SourcePaths.Overrides),
        DevNameMappings.Load(SourcePaths.DevNameMappings));
}

/// <summary>
/// Composes the loaded sources into a list of merged, enriched spells for every hero.
/// </summary>
public static class MergeEngine
{
    /// <summary>
    /// For each hero, selects their Kit abilities (filtered to the hero's own DevKey prefix),
    /// resolves scalars from matching Constants entries, links and includes spawned effects,
    /// and enriches each entry with name/kind from <c>spell_data</c>, icon from <c>abilities.json</c>,
    /// and costs from the merged scalar bag.
    /// </summary>
    public static MergeResult Run(MergeInputs inputs)
    {
        var spells = new List<MergedSpell>();

        foreach (var hero in inputs.Heroes.Heroes)
        {
            var scope = hero.DisplayName.ToLowerInvariant();
            var heroPrefix = $"GA_{hero.DevKey}_";
            var effectLinks = Linking.LinkEffects(hero, inputs.Spells);
            var linksByAbilityFslId = effectLinks
                .GroupBy(l => l.AbilityFslId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kit in hero.Kit)
            {
                if (!kit.DevName.StartsWith(heroPrefix, StringComparison.Ordinal))
                    continue;

                var kind = SpellKindRange.FromFslId(kit.FslId);
                var nativeId = SpellKindRange.NativeId(kit.FslId);

                var constants = Linking.ConstantsFor(kit, hero);
                var scalars = MergeScalars(constants);

                var name = ResolveAbilityName(nativeId, kind, kit.Name, inputs.Spells);
                var member = MemberNaming.Sanitize(name);
                var icon = inputs.Icons.IconFor(SpellKindRange.GuidFor(kind, nativeId)) ?? string.Empty;
                var costs = Costs.Map(scalars, hero.Resources);

                spells.Add(new MergedSpell(
                    scope, member, nativeId, kind, name, icon,
                    Normalization.Cooldown(scalars), Normalization.Range(scalars),
                    Normalization.Charges(scalars), Normalization.CastDuration(scalars),
                    Normalization.ChannelDuration(scalars), Normalization.ChannelTickInterval(scalars),
                    costs, new Provenance()));

                if (!linksByAbilityFslId.TryGetValue(kit.FslId, out var links))
                    continue;

                foreach (var link in links)
                {
                    var effectKind = SpellKindRange.FromFslId(link.EffectFslId);
                    var effectId = SpellKindRange.NativeId(link.EffectFslId);
                    var effectName = inputs.Spells.Effects.TryGetValue(effectId, out var effectEntry)
                        ? effectEntry.Name ?? string.Empty
                        : string.Empty;
                    var effectMember = MemberNaming.EffectMember(member, link.Role);
                    var effectIcon = inputs.Icons.IconFor(SpellKindRange.GuidFor(effectKind, effectId)) ?? string.Empty;

                    spells.Add(new MergedSpell(
                        scope, effectMember, effectId, effectKind, effectName, effectIcon,
                        null, null, 1, null, null, null,
                        new Dictionary<string, int>(), new Provenance()));
                }
            }
        }

        return new MergeResult(spells, []);
    }

    private static string ResolveAbilityName(int nativeId, SpellKind kind, string? kitName, SpellDataSource spells)
    {
        if (kind == SpellKind.Ability && spells.Abilities.TryGetValue(nativeId, out var entry) && entry.Name is not null)
            return entry.Name;
        return kitName ?? string.Empty;
    }

    private static Dictionary<string, double> MergeScalars(IReadOnlyList<ConstantsEntry> constants)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in constants)
            foreach (var (key, value) in c.Scalars)
                result[key] = value;
        return result;
    }
}
