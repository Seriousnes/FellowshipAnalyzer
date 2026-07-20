using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
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
            if (!HeroNames.Contains(scope))
                gaps.Add(new Gap(scope, scope, GapKind.UnknownScope));
            foreach (var unknownResource in hero.Resources.UnknownResourceNames)
                gaps.Add(new Gap(scope, unknownResource, GapKind.UnknownResource));
            var heroPrefix = $"GA_{hero.DevKey}_";
            var effectLinks = Linking.LinkEffects(hero, inputs.SpellData);
            var linksByAbilityFslId = effectLinks
                .GroupBy(l => l.AbilityFslId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kit in hero.Kit)
            {
                if (!kit.DevName.StartsWith(heroPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var kind = new FSLID(kit.FslId).Kind;
                var nativeId = new FSLID(kit.FslId).NativeId;

                var constants = Linking.ConstantsFor(kit, hero);
                var scalars = MergeScalars(constants);

                string? spellDataName = null;
                if (kind == SpellKind.Ability && inputs.SpellData.Abilities.TryGetValue(nativeId, out var abilityEntry))
                    spellDataName = abilityEntry.Name;

                var name = spellDataName ?? kit.Name ?? string.Empty;
                var member = MemberNaming.Sanitize(name);
                var guid = FSLID.FromNative(kind, nativeId);
                var icon = inputs.Icons.IconFor(guid) ?? string.Empty;
                var costs = Costs.Map(scalars, hero.Resources);

                var cooldown = Normalization.Cooldown(scalars);
                var cooldownReductionOnTargetDeath = Normalization.CooldownReductionOnTargetDeath(scalars);
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
                    .SetIf("cooldownReductionOnTargetDeath", cooldownReductionOnTargetDeath.HasValue, ProvenanceSource.HeroData)
                    .SetIf("range", range.HasValue, ProvenanceSource.HeroData)
                    .Set("charges", ProvenanceSource.HeroData)
                    .SetIf("castDuration", castDuration.HasValue, ProvenanceSource.HeroData)
                    .SetIf("channelDuration", channelDuration.HasValue, ProvenanceSource.HeroData)
                    .SetIf("channelTickInterval", channelTickInterval.HasValue, ProvenanceSource.HeroData)
                    .SetIf("costs", costs.Count > 0, ProvenanceSource.HeroData)
                    .Build();

                var spell = BuildSpell(kind, nativeId, name, icon, cooldown, cooldownReductionOnTargetDeath,
                    range, charges, castDuration, channelDuration, channelTickInterval, costs);
                spells.Add(new CuratedSpell(scope, member, spell, prov));

                if (!MemberNaming.IsValidIdentifier(member))
                    gaps.Add(new Gap(scope, member, GapKind.MissingName));
                if (string.IsNullOrEmpty(icon))
                    gaps.Add(new Gap(scope, member, GapKind.MissingIcon));

                if (!linksByAbilityFslId.TryGetValue(kit.FslId, out var links))
                    continue;

                foreach (var link in links)
                {
                    var effectKind = new FSLID(link.EffectFslId).Kind;
                    var effectId = new FSLID(link.EffectFslId).NativeId;
                    var effectName = inputs.SpellData.Effects.TryGetValue(effectId, out var effectEntry)
                        ? effectEntry.Name ?? string.Empty
                        : string.Empty;
                    var effectMember = MemberNaming.EffectMember(member, link.Role);
                    var effectIcon = inputs.Icons.IconFor(FSLID.FromNative(effectKind, effectId)) ?? string.Empty;

                    var effectProv = new ProvenanceBuilder()
                        .Set("id", ProvenanceSource.HeroData)
                        .Set("kind", ProvenanceSource.SpellData)
                        .SetIf("name", effectEntry?.Name is not null, ProvenanceSource.SpellData)
                        .SetIf("icon", effectIcon.Length > 0, ProvenanceSource.Icons)
                        .Set("charges", ProvenanceSource.HeroData)
                        .Build();

                    var effectSpell = BuildSpell(effectKind, effectId, effectName, effectIcon,
                        null, null, null, 1, null, null, null, EmptyCosts);
                    spells.Add(new CuratedSpell(scope, effectMember, effectSpell, effectProv));

                    if (!MemberNaming.IsValidIdentifier(effectMember))
                        gaps.Add(new Gap(scope, effectMember, GapKind.MissingName));
                }
            }

            var linkedEffectFslIds = new HashSet<int>(effectLinks.Select(l => l.EffectFslId));
            var heroEffectPrefix = $"GE_{hero.DevKey}_";
            foreach (var effect in inputs.SpellData.Effects.Values)
            {
                if (!effect.DevName.StartsWith(heroEffectPrefix, StringComparison.OrdinalIgnoreCase))
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
                var overrideId = DeltaId(delta);
                if (overrideId is int oid)
                {
                    var ovKind = DeltaKind(delta) ?? new FSLID(oid).Kind;
                    var targetGuid = FSLID.FromNative(ovKind, new FSLID(oid).NativeId);
                    spells.RemoveAll(s => s.Scope == scope && s.Member != member && s.FSLID.Value == targetGuid.Value);
                }

                var idx = spells.FindIndex(s => s.Scope == scope && s.Member == member);
                if (idx >= 0)
                {
                    spells[idx] = ApplyPatch(spells[idx], delta);
                }
                else if (overrideId is null)
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

    private static readonly HashSet<string> HeroNames =
        new(Enum.GetNames<HeroName>(), StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<ResourceTypes, int> EmptyCosts =
        new Dictionary<ResourceTypes, int>();

    private static Spell BuildSpell(
        SpellKind kind, int nativeId, string name, string icon,
        double? cooldown, double? cooldownReductionOnTargetDeath, int? range, int charges, double? castDuration,
        double? channelDuration, double? channelTickInterval, IReadOnlyDictionary<ResourceTypes, int> costs) =>
        Spell.FromFSLID(FSLID.FromNative(kind, nativeId), name, icon) with
        {
            Cooldown = cooldown,
            CooldownReductionOnTargetDeath = cooldownReductionOnTargetDeath,
            Range = range,
            Charges = charges,
            CastDuration = castDuration,
            ChannelDuration = channelDuration,
            ChannelTickInterval = channelTickInterval,
            Costs = costs,
        };

    private static CuratedSpell ApplyPatch(CuratedSpell curated, JsonObject delta)
    {
        var node = JsonSerializer.SerializeToNode(curated.Spell, SpellDbJsonOptions.Default)!.AsObject();
        foreach (var (key, value) in delta)
        {
            if (key == "note")
                continue;
            node[key] = value?.DeepClone();
        }
        var spell = node.Deserialize<Spell>(SpellDbJsonOptions.Default)!;

        var prov = new Dictionary<string, ProvenanceSource>(curated.Provenance.ByField, StringComparer.Ordinal);
        foreach (var (key, _) in delta)
            if (key != "note")
                prov[key] = ProvenanceSource.Override;

        return curated with { Spell = spell, Provenance = new Provenance(prov) };
    }

    private static int? DeltaId(JsonObject delta) =>
        delta.TryGetPropertyValue("id", out var v) && v is JsonValue jv && jv.TryGetValue<int>(out var id) ? id : null;

    private static SpellKind? DeltaKind(JsonObject delta) =>
        delta.TryGetPropertyValue("kind", out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s)
            && Enum.TryParse<SpellKind>(s, ignoreCase: true, out var kind) ? kind : null;

    private static (CuratedSpell Spell, IEnumerable<Gap> Gaps) EnrichById(
        string scope, string member, JsonObject delta, MergeInputs inputs)
    {
        var id = DeltaId(delta)!.Value;
        var kind = DeltaKind(delta) ?? new FSLID(id).Kind;
        var nativeId = new FSLID(id).NativeId;
        var scalars = GatherScalarsById(id, inputs);

        string? nameFromSpellData = null;
        if (kind == SpellKind.Ability && inputs.SpellData.Abilities.TryGetValue(nativeId, out var abilityEntry))
            nameFromSpellData = abilityEntry.Name;
        else if (kind == SpellKind.Effect && inputs.SpellData.Effects.TryGetValue(nativeId, out var effectEntry))
            nameFromSpellData = effectEntry.Name;

        var gearWeapon = inputs.GearData.Weapons.FirstOrDefault(w => w.FslId == id)
            ?? inputs.GearData.WeaponTraits.FirstOrDefault(w => w.FslId == id);

        var resolvedName = nameFromSpellData ?? gearWeapon?.DisplayName ?? string.Empty;
        var guid = FSLID.FromNative(kind, nativeId);
        var icon = inputs.Icons.IconFor(guid) ?? string.Empty;
        var costs = Costs.Map(scalars, new ResourceModel(new Dictionary<string, ResourceTypes>(), []));

        var cooldown = Normalization.Cooldown(scalars);
        var cooldownReductionOnTargetDeath = Normalization.CooldownReductionOnTargetDeath(scalars);
        var range = Normalization.Range(scalars);
        var charges = Normalization.Charges(scalars);
        var castDuration = Normalization.CastDuration(scalars);
        var channelDuration = Normalization.ChannelDuration(scalars);
        var channelTickInterval = Normalization.ChannelTickInterval(scalars);

        var nameSource = nameFromSpellData is not null ? ProvenanceSource.SpellData
            : gearWeapon is not null ? ProvenanceSource.GearData
            : (ProvenanceSource?)null;

        var prov = new ProvenanceBuilder()
            .Set("id", ProvenanceSource.SpellData)
            .Set("kind", ProvenanceSource.SpellData);
        if (nameSource is { } ns) prov.Set("name", ns);
        prov.SetIf("icon", icon.Length > 0, ProvenanceSource.Icons);
        prov.SetIf("cooldown", cooldown.HasValue, ProvenanceSource.GearData);
        prov.SetIf("cooldownReductionOnTargetDeath", cooldownReductionOnTargetDeath.HasValue, ProvenanceSource.GearData);
        prov.SetIf("range", range.HasValue, ProvenanceSource.GearData);
        prov.Set("charges", ProvenanceSource.GearData);
        prov.SetIf("castDuration", castDuration.HasValue, ProvenanceSource.GearData);
        prov.SetIf("channelDuration", channelDuration.HasValue, ProvenanceSource.GearData);
        prov.SetIf("channelTickInterval", channelTickInterval.HasValue, ProvenanceSource.GearData);
        prov.SetIf("costs", costs.Count > 0, ProvenanceSource.GearData);

        var baseSpell = BuildSpell(kind, nativeId, resolvedName, icon, cooldown, cooldownReductionOnTargetDeath,
            range, charges, castDuration, channelDuration, channelTickInterval, costs);
        var curated = ApplyPatch(new CuratedSpell(scope, member, baseSpell, prov.Build()), delta);

        var addedGaps = new List<Gap>();
        if (!MemberNaming.IsValidIdentifier(curated.Member))
            addedGaps.Add(new Gap(scope, curated.Member, GapKind.MissingName));
        if (string.IsNullOrEmpty(curated.Spell.Icon))
            addedGaps.Add(new Gap(scope, curated.Member, GapKind.MissingIcon));

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
