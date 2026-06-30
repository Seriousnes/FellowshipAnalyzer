using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// All upstream data sources loaded and ready for <see cref="MergeEngine.Run"/>.
/// Declared as a record so callers can produce patched variants via <c>with { … }</c>.
/// </summary>
public record MergeInputs(
    SpellDataSource SpellData,
    GearDataSource GearData,
    HeroDataSource HeroData,
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
/// Composes the loaded sources into a list of curated Core <see cref="Spell"/> instances for every hero.
/// </summary>
public static class MergeEngine
{
    /// <summary>
    /// For each hero in <c>hero_data.json</c>, selects Kit abilities, resolves scalars from Constants,
    /// links spawned effects, and enriches each entry with name/kind from <c>spell_data</c>, icon from
    /// <c>abilities.json</c>, and costs from the merged scalar bag. After auto-selection, applies overrides.
    /// </summary>
    public static MergeResult Run(MergeInputs inputs)
    {
        var spells = new List<CuratedSpell>();
        var gaps = new List<Gap>();

        foreach (var hero in inputs.HeroData.Heroes)
        {
            var scope = hero.DisplayName.ToLowerInvariant();
            var heroPrefix = $"GA_{hero.DevKey}_";
            var effectLinks = Linking.LinkEffects(hero, inputs.SpellData);
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

                string? spellDataName = null;
                if (kind == SpellKind.Ability && inputs.SpellData.Abilities.TryGetValue(nativeId, out var abilityEntry))
                    spellDataName = abilityEntry.Name;

                var name = spellDataName ?? kit.Name ?? string.Empty;
                var member = MemberNaming.Sanitize(name);
                var guid = SpellKindRange.GuidFor(kind, nativeId);
                var icon = inputs.Icons.IconFor(guid) ?? string.Empty;
                var costs = Costs.Map(scalars, hero.Resources);

                var cooldown = Normalization.Cooldown(scalars);
                var range = Normalization.Range(scalars);
                var charges = Normalization.Charges(scalars);
                var castDuration = Normalization.CastDuration(scalars);
                var channelDuration = Normalization.ChannelDuration(scalars);
                var channelTickInterval = Normalization.ChannelTickInterval(scalars);

                var prov = new ProvenanceBuilder()
                    .Set("id", ProvenanceSource.HeroData)
                    .Set("kind", ProvenanceSource.HeroData)
                    .Set("name", spellDataName is not null ? ProvenanceSource.SpellData : ProvenanceSource.HeroData)
                    .SetIf("icon", icon.Length > 0, ProvenanceSource.Icons)
                    .SetIf("cooldown", cooldown.HasValue, ProvenanceSource.HeroData)
                    .SetIf("range", range.HasValue, ProvenanceSource.HeroData)
                    .Set("charges", ProvenanceSource.HeroData)
                    .SetIf("castDuration", castDuration.HasValue, ProvenanceSource.HeroData)
                    .SetIf("channelDuration", channelDuration.HasValue, ProvenanceSource.HeroData)
                    .SetIf("channelTickInterval", channelTickInterval.HasValue, ProvenanceSource.HeroData)
                    .SetIf("costs", costs.Count > 0, ProvenanceSource.HeroData)
                    .Build();

                var spell = BuildSpell(kind, nativeId, name, icon, cooldown, range, charges,
                    castDuration, channelDuration, channelTickInterval, costs);
                spells.Add(new CuratedSpell(scope, member, spell, prov));

                if (!MemberNaming.IsValidIdentifier(member))
                    gaps.Add(new Gap(scope, member, GapKind.MissingName));
                if (string.IsNullOrEmpty(icon))
                    gaps.Add(new Gap(scope, member, GapKind.MissingIcon));

                if (!linksByAbilityFslId.TryGetValue(kit.FslId, out var links))
                    continue;

                foreach (var link in links)
                {
                    var effectKind = SpellKindRange.FromFslId(link.EffectFslId);
                    var effectId = SpellKindRange.NativeId(link.EffectFslId);
                    var effectName = inputs.SpellData.Effects.TryGetValue(effectId, out var effectEntry)
                        ? effectEntry.Name ?? string.Empty
                        : string.Empty;
                    var effectMember = MemberNaming.EffectMember(member, link.Role);
                    var effectIcon = inputs.Icons.IconFor(SpellKindRange.GuidFor(effectKind, effectId)) ?? string.Empty;

                    var effectProv = new ProvenanceBuilder()
                        .Set("id", ProvenanceSource.HeroData)
                        .Set("kind", ProvenanceSource.SpellData)
                        .SetIf("name", effectEntry?.Name is not null, ProvenanceSource.SpellData)
                        .SetIf("icon", effectIcon.Length > 0, ProvenanceSource.Icons)
                        .Set("charges", ProvenanceSource.HeroData)
                        .Build();

                    var effectSpell = BuildSpell(effectKind, effectId, effectName, effectIcon,
                        null, null, 1, null, null, null, EmptyCosts);
                    spells.Add(new CuratedSpell(scope, effectMember, effectSpell, effectProv));

                    if (!MemberNaming.IsValidIdentifier(effectMember))
                        gaps.Add(new Gap(scope, effectMember, GapKind.MissingName));
                }
            }

            var linkedEffectFslIds = new HashSet<int>(effectLinks.Select(l => l.EffectFslId));
            var heroEffectPrefix = $"GE_{hero.DevKey}_";
            foreach (var effect in inputs.SpellData.Effects.Values)
            {
                if (!effect.DevName.StartsWith(heroEffectPrefix, StringComparison.Ordinal))
                    continue;
                if (effect.Name is null || effect.FslId == 0)
                    continue;
                if (linkedEffectFslIds.Contains(effect.FslId))
                    continue;
                gaps.Add(new Gap(scope, MemberNaming.Sanitize(effect.Name), GapKind.UnresolvedEffect));
            }
        }

        foreach (var (scope, members) in inputs.Overrides.ByScopeAndMember)
        {
            foreach (var (member, delta) in members)
            {
                if (delta.Id is int overrideId)
                {
                    var ovKind = delta.Kind ?? SpellKindRange.FromFslId(overrideId);
                    var targetGuid = SpellKindRange.GuidFor(ovKind, SpellKindRange.NativeId(overrideId));
                    spells.RemoveAll(s => s.Scope == scope && s.Member != member && s.Guid == targetGuid);
                }

                var idx = spells.FindIndex(s => s.Scope == scope && s.Member == member);
                if (idx >= 0)
                {
                    spells[idx] = ApplyPatch(spells[idx], delta);
                }
                else if (delta.Id is null)
                {
                    gaps.Add(new Gap(scope, member, GapKind.MissingId));
                }
                else
                {
                    var (added, addedGaps) = EnrichById(scope, member, delta, inputs);
                    spells.Add(added);
                    gaps.AddRange(addedGaps);
                }
            }
        }

        return new MergeResult(spells, gaps);
    }

    private static readonly IReadOnlyDictionary<ResourceTypes, int> EmptyCosts =
        new Dictionary<ResourceTypes, int>();

    private static Spell BuildSpell(
        SpellKind kind, int nativeId, string name, string icon,
        double? cooldown, int? range, int charges, double? castDuration,
        double? channelDuration, double? channelTickInterval, IReadOnlyDictionary<ResourceTypes, int> costs) =>
        Spell.FromGuid(SpellKindRange.GuidFor(kind, nativeId), name, icon) with
        {
            Cooldown = cooldown,
            Range = range,
            Charges = charges,
            CastDuration = castDuration,
            ChannelDuration = channelDuration,
            ChannelTickInterval = channelTickInterval,
            Costs = costs,
        };

    private static CuratedSpell ApplyPatch(CuratedSpell curated, OverrideEntry delta)
    {
        var s = curated.Spell;
        var newKind = delta.Kind ?? curated.Kind;
        var newId = delta.Id ?? s.Id;
        var costs = delta.Costs.Count > 0 ? delta.Costs : s.Costs;

        var spell = BuildSpell(newKind, newId,
            delta.Name ?? s.Name,
            delta.Icon ?? s.Icon,
            delta.Cooldown ?? s.Cooldown,
            delta.Range ?? s.Range,
            delta.Charges ?? s.Charges,
            delta.CastDuration ?? s.CastDuration,
            delta.ChannelDuration ?? s.ChannelDuration,
            delta.ChannelTickInterval ?? s.ChannelTickInterval,
            costs);

        var prov = new Dictionary<string, ProvenanceSource>(curated.Provenance.ByField, StringComparer.Ordinal);
        MarkOverride(prov, "id", delta.Id.HasValue);
        MarkOverride(prov, "kind", delta.Kind.HasValue);
        MarkOverride(prov, "name", delta.Name is not null);
        MarkOverride(prov, "icon", delta.Icon is not null);
        MarkOverride(prov, "cooldown", delta.Cooldown.HasValue);
        MarkOverride(prov, "range", delta.Range.HasValue);
        MarkOverride(prov, "charges", delta.Charges.HasValue);
        MarkOverride(prov, "castDuration", delta.CastDuration.HasValue);
        MarkOverride(prov, "channelDuration", delta.ChannelDuration.HasValue);
        MarkOverride(prov, "channelTickInterval", delta.ChannelTickInterval.HasValue);
        MarkOverride(prov, "costs", delta.Costs.Count > 0);

        return curated with { Spell = spell, Provenance = new Provenance(prov) };
    }

    private static void MarkOverride(Dictionary<string, ProvenanceSource> prov, string field, bool present)
    {
        if (present)
            prov[field] = ProvenanceSource.Override;
    }

    private static (CuratedSpell Spell, IEnumerable<Gap> Gaps) EnrichById(
        string scope, string member, OverrideEntry delta, MergeInputs inputs)
    {
        var id = delta.Id!.Value;
        var kind = delta.Kind ?? SpellKindRange.FromFslId(id);
        var nativeId = SpellKindRange.NativeId(id);
        var scalars = GatherScalarsById(id, inputs);

        string? nameFromSpellData = null;
        if (kind == SpellKind.Ability && inputs.SpellData.Abilities.TryGetValue(nativeId, out var abilityEntry))
            nameFromSpellData = abilityEntry.Name;
        else if (kind == SpellKind.Effect && inputs.SpellData.Effects.TryGetValue(nativeId, out var effectEntry))
            nameFromSpellData = effectEntry.Name;

        var gearWeapon = inputs.GearData.Weapons.FirstOrDefault(w => w.FslId == id)
            ?? inputs.GearData.WeaponTraits.FirstOrDefault(w => w.FslId == id);

        var resolvedName = delta.Name ?? nameFromSpellData ?? gearWeapon?.DisplayName ?? string.Empty;
        var guid = SpellKindRange.GuidFor(kind, nativeId);
        var icon = delta.Icon ?? inputs.Icons.IconFor(guid) ?? string.Empty;

        var cooldown = delta.Cooldown ?? Normalization.Cooldown(scalars);
        var range = delta.Range ?? Normalization.Range(scalars);
        var charges = delta.Charges ?? Normalization.Charges(scalars);
        var castDuration = delta.CastDuration ?? Normalization.CastDuration(scalars);
        var channelDuration = delta.ChannelDuration ?? Normalization.ChannelDuration(scalars);
        var channelTickInterval = delta.ChannelTickInterval ?? Normalization.ChannelTickInterval(scalars);

        var costs = delta.Costs.Count > 0
            ? delta.Costs
            : Costs.Map(scalars, new ResourceModel(new Dictionary<string, ResourceTypes>()));

        var nameSource = delta.Name is not null ? ProvenanceSource.Override
            : nameFromSpellData is not null ? ProvenanceSource.SpellData
            : gearWeapon is not null ? ProvenanceSource.GearData
            : (ProvenanceSource?)null;

        var prov = new ProvenanceBuilder()
            .Set("id", ProvenanceSource.Override)
            .Set("kind", delta.Kind.HasValue ? ProvenanceSource.Override : ProvenanceSource.SpellData);
        if (nameSource is { } ns) prov.Set("name", ns);
        prov.Set("icon", delta.Icon is not null ? ProvenanceSource.Override : ProvenanceSource.Icons);
        prov.SetIf("cooldown", cooldown.HasValue, delta.Cooldown.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData);
        prov.SetIf("range", range.HasValue, delta.Range.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData);
        prov.Set("charges", delta.Charges.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData);
        prov.SetIf("castDuration", castDuration.HasValue, delta.CastDuration.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData);
        prov.SetIf("channelDuration", channelDuration.HasValue, delta.ChannelDuration.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData);
        prov.SetIf("channelTickInterval", channelTickInterval.HasValue, delta.ChannelTickInterval.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData);
        prov.SetIf("costs", costs.Count > 0, delta.Costs.Count > 0 ? ProvenanceSource.Override : ProvenanceSource.GearData);

        var spell = BuildSpell(kind, nativeId, resolvedName, icon, cooldown, range, charges,
            castDuration, channelDuration, channelTickInterval, costs);
        var curated = new CuratedSpell(scope, member, spell, prov.Build());

        var addedGaps = new List<Gap>();
        if (!MemberNaming.IsValidIdentifier(member))
            addedGaps.Add(new Gap(scope, member, GapKind.MissingName));
        if (string.IsNullOrEmpty(icon))
            addedGaps.Add(new Gap(scope, member, GapKind.MissingIcon));

        return (curated, addedGaps);
    }

    private static Dictionary<string, double> GatherScalarsById(int id, MergeInputs inputs)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);

        var gearWeapon = inputs.GearData.Weapons.FirstOrDefault(w => w.FslId == id)
            ?? inputs.GearData.WeaponTraits.FirstOrDefault(w => w.FslId == id);
        if (gearWeapon is not null)
            foreach (var (k, v) in gearWeapon.Scalars)
                result[k] = v;

        foreach (var hero in inputs.HeroData.Heroes)
        {
            var kit = hero.Kit.FirstOrDefault(k => k.FslId == id);
            if (kit is null)
                continue;
            foreach (var (k, v) in MergeScalars(Linking.ConstantsFor(kit, hero)))
                result[k] = v;
            break;
        }

        return result;
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
