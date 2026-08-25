using System.Text.Json;
using System.Text.Json.Nodes;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// All upstream data sources loaded and ready for <see cref="MergeEngine.Run"/>.
/// Declared as a record so callers can produce patched variants via <c>with { … }</c>.
/// </summary>
public record MergeInputs(
    ExportSource Export,
    IconSource Icons,
    OverridesSource Overrides)
{
    /// <summary>Loads every upstream source from the committed file paths.</summary>
    public static MergeInputs Load() => new(
        ExportSource.Load(SourcePaths.Entities, SourcePaths.Settings),
        IconSource.Load(SourcePaths.Abilities),
        OverridesSource.Load(SourcePaths.Overrides));
}

/// <summary>
/// Composes the loaded sources into a list of curated Core <see cref="Spell"/> instances for every hero.
/// </summary>
public static class MergeEngine
{
    /// <summary>
    /// Selects every export ability into the scope that owns it: a <see cref="AbilityCategory.Relic"/> or
    /// <see cref="AbilityCategory.Weapon"/> ability is granted by equipment, so it lands once in <c>items</c>;
    /// every other ability lands in each hero scope its <c>heroes</c> array names. Each effect the export marks
    /// as <c>partOf</c> an ability follows that ability into the same scope, and both are enriched with an icon
    /// from <c>abilities.json</c>. After auto-selection, applies overrides.
    /// </summary>
    public static MergeResult Run(MergeInputs inputs)
    {
        var spells = new List<CuratedSpell>();
        var gaps = new List<Gap>();

        var effectsByAbility = inputs.Export.Effects.Values
            .Where(e => e.PartOf is { Type: "ability" })
            .GroupBy(e => e.PartOf!.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Id).ToList());

        foreach (var (scope, abilities) in RouteAbilities(inputs.Export, gaps))
        {
            foreach (var ability in abilities)
            {
                if (string.IsNullOrEmpty(ability.Name))
                {
                    gaps.Add(new Gap(scope, $"ability {ability.Id}", GapKind.MissingName));
                    continue;
                }

                var member = MemberNaming.Sanitize(ability.Name);
                var guid = FSLID.FromNative(SpellKind.Ability, ability.Id);
                var icon = inputs.Icons.IconFor(guid) ?? string.Empty;
                var costs = Costs.Map(ability, gaps, scope, member);
                var abilityCategory = inputs.Export.CategoryFor(ability.Category);

                var prov = new ProvenanceBuilder()
                    .Set("id", ProvenanceSource.Export)
                    .Set("kind", ProvenanceSource.Export)
                    .Set("name", ProvenanceSource.Export)
                    .SetIf("icon", icon.Length > 0, ProvenanceSource.Icons)
                    .SetIf("abilityCategory", abilityCategory.HasValue, ProvenanceSource.Export)
                    .SetIf("cooldown", ability.Cooldown.HasValue, ProvenanceSource.Export)
                    .SetIf("range", ability.Range.HasValue, ProvenanceSource.Export)
                    .SetIf("radius", ability.Radius.HasValue, ProvenanceSource.Export)
                    .Set("charges", ProvenanceSource.Export)
                    .SetIf("castDuration", ability.CastTime.HasValue, ProvenanceSource.Export)
                    .SetIf("channelDuration", ability.ChannelTime.HasValue, ProvenanceSource.Export)
                    .SetIf("channelTickInterval", ability.ChannelTick.HasValue, ProvenanceSource.Export)
                    .SetIf("costs", costs.Count > 0, ProvenanceSource.Export)
                    .Build();

                var spell = BuildSpell(SpellKind.Ability, ability.Id, ability.Name, icon,
                    cooldown: ability.Cooldown,
                    cooldownReductionOnTargetDeath: null,
                    range: ability.Range,
                    radius: ability.Radius,
                    charges: ability.ChargeCount,
                    castDuration: ability.CastTime,
                    channelDuration: ability.ChannelTime,
                    channelTickInterval: ability.ChannelTick,
                    costs: costs,
                    abilityCategory: abilityCategory);
                spells.Add(new CuratedSpell(scope, member, spell, prov));

                if (!MemberNaming.IsValidIdentifier(member))
                    gaps.Add(new Gap(scope, member, GapKind.MissingName));
                if (string.IsNullOrEmpty(icon))
                    gaps.Add(new Gap(scope, member, GapKind.MissingIcon));

                if (!effectsByAbility.TryGetValue(ability.Id, out var effects))
                    continue;

                foreach (var effect in effects)
                {
                    if (string.IsNullOrEmpty(effect.Role))
                    {
                        gaps.Add(new Gap(scope, $"{member} (effect {effect.Id})", GapKind.UnresolvedEffect));
                        continue;
                    }

                    var effectMember = MemberNaming.EffectMember(member, effect.Role);
                    var effectIcon = inputs.Icons.IconFor(FSLID.FromNative(SpellKind.Effect, effect.Id)) ?? string.Empty;

                    var effectProv = new ProvenanceBuilder()
                        .Set("id", ProvenanceSource.Export)
                        .Set("kind", ProvenanceSource.Export)
                        .SetIf("name", effect.Name is not null, ProvenanceSource.Export)
                        .SetIf("icon", effectIcon.Length > 0, ProvenanceSource.Icons)
                        .Set("charges", ProvenanceSource.Export)
                        .Build();

                    var effectSpell = BuildSpell(SpellKind.Effect, effect.Id, effect.Name ?? string.Empty, effectIcon,
                        cooldown: null,
                        cooldownReductionOnTargetDeath: null,
                        range: null,
                        radius: null,
                        charges: 1,
                        castDuration: null,
                        channelDuration: null,
                        channelTickInterval: null,
                        costs: EmptyCosts);
                    spells.Add(new CuratedSpell(scope, effectMember, effectSpell, effectProv));

                    if (!MemberNaming.IsValidIdentifier(effectMember))
                        gaps.Add(new Gap(scope, effectMember, GapKind.MissingName));
                }
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

        foreach (var (scope, members) in inputs.Overrides.ByScopeAndMember)
        {
            foreach (var (member, delta) in members)
            {
                if (!ClaimsSoleHero(delta) || ResolveClaim(spells, scope, member, delta) is not FSLID claimed)
                    continue;

                foreach (var guid in ClaimedWithLinkedEffects(claimed, effectsByAbility))
                    spells.RemoveAll(s => s.Scope != scope && s.FSLID.Value == guid.Value);
            }
        }

        return new MergeResult(spells, gaps) { Schools = BuildSchools(inputs, spells) };
    }

    private static List<(string Scope, List<ExportAbility> Abilities)> RouteAbilities(ExportSource export, List<Gap> gaps)
    {
        var routed = new List<(string Scope, List<ExportAbility> Abilities)>();
        var gearGranted = new List<ExportAbility>();
        var gearSeen = new HashSet<int>();

        foreach (var hero in export.Heroes)
        {
            var scope = hero.Name.ToLowerInvariant();
            if (!HeroNames.Contains(scope))
                gaps.Add(new Gap(scope, scope, GapKind.UnknownScope));

            var kit = new List<ExportAbility>();
            foreach (var ability in export.Abilities.Values
                .Where(a => a.Heroes.Contains(hero.Name, StringComparer.Ordinal))
                .OrderBy(a => a.Id))
            {
                if (export.CategoryFor(ability.Category) is AbilityCategory.Relic or AbilityCategory.Weapon)
                {
                    if (gearSeen.Add(ability.Id))
                        gearGranted.Add(ability);
                }
                else
                {
                    kit.Add(ability);
                }
            }

            routed.Add((scope, kit));
        }

        routed.Add((ItemsScope, [.. gearGranted.OrderBy(a => a.Id)]));
        return routed;
    }

    /// <summary>
    /// Collects every classified damage school in the export into one FSLID-keyed map, then lets any
    /// curated spell carrying a <see cref="Spell.School"/> override what the export gave that id.
    /// </summary>
    private static Dictionary<int, MagicSchool> BuildSchools(MergeInputs inputs, List<CuratedSpell> spells)
    {
        var schools = new Dictionary<int, MagicSchool>();

        foreach (var ability in inputs.Export.Abilities.Values)
        {
            var school = Schools.FromExport(ability.Schools);
            if (school != default)
                schools[FSLID.FromNative(SpellKind.Ability, ability.Id).Value] = school;
        }

        foreach (var effect in inputs.Export.Effects.Values)
        {
            var school = Schools.FromExport(effect.Schools);
            if (school != default)
                schools[FSLID.FromNative(SpellKind.Effect, effect.Id).Value] = school;
        }

        foreach (var curated in spells)
            if (curated.Spell.School is { } school && school != default)
                schools[curated.FSLID.Value] = school;

        return schools;
    }

    private const string ItemsScope = "items";

    private const string SoleHeroKey = "soleHero";

    private static readonly HashSet<string> CurationKeys = new(StringComparer.Ordinal) { SoleHeroKey };

    private static readonly HashSet<string> HeroNames =
        new(Enum.GetNames<HeroName>(), StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<ResourceTypes, int> EmptyCosts =
        [];

    private static Spell BuildSpell(
        SpellKind kind, int nativeId, string name, string icon,
        double? cooldown, double? cooldownReductionOnTargetDeath, int? range, int? radius, int charges,
        double? castDuration, double? channelDuration, double? channelTickInterval,
        Dictionary<ResourceTypes, int> costs,
        AbilityCategory? abilityCategory = null) =>
        Spell.FromFSLID(FSLID.FromNative(kind, nativeId), name, icon) with
        {
            AbilityCategory = abilityCategory,
            Cooldown = cooldown,
            CooldownReductionOnTargetDeath = cooldownReductionOnTargetDeath,
            Range = range,
            Radius = radius,
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
            if (CurationKeys.Contains(key))
                continue;
            node[key] = value?.DeepClone();
        }
        var spell = node.Deserialize<Spell>(SpellDbJsonOptions.Default)!;

        var prov = new Dictionary<string, ProvenanceSource>(curated.Provenance.ByField, StringComparer.Ordinal);
        foreach (var (key, _) in delta)
            if (!CurationKeys.Contains(key))
                prov[key] = ProvenanceSource.Override;

        return curated with { Spell = spell, Provenance = new Provenance(prov) };
    }

    private static bool ClaimsSoleHero(JsonObject delta) =>
        delta.TryGetPropertyValue(SoleHeroKey, out var v) && v is JsonValue jv
            && jv.TryGetValue<bool>(out var claimed) && claimed;

    private static FSLID? ResolveClaim(List<CuratedSpell> spells, string scope, string member, JsonObject delta)
    {
        if (DeltaId(delta) is int id)
            return FSLID.FromNative(DeltaKind(delta) ?? new FSLID(id).Kind, new FSLID(id).NativeId);

        var idx = spells.FindIndex(s => s.Scope == scope && s.Member == member);
        return idx >= 0 ? spells[idx].FSLID : null;
    }

    private static IEnumerable<FSLID> ClaimedWithLinkedEffects(
        FSLID claimed, Dictionary<int, List<ExportEffect>> effectsByAbility)
    {
        yield return claimed;

        if (claimed.Kind != SpellKind.Ability || !effectsByAbility.TryGetValue(claimed.NativeId, out var effects))
            yield break;

        foreach (var effect in effects)
            yield return FSLID.FromNative(SpellKind.Effect, effect.Id);
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

        ExportAbility? ability = null;
        string? exportName = null;
        if (kind == SpellKind.Ability && inputs.Export.Abilities.TryGetValue(nativeId, out var abilityRecord))
        {
            ability = abilityRecord;
            exportName = abilityRecord.Name;
        }
        else if (kind == SpellKind.Effect && inputs.Export.Effects.TryGetValue(nativeId, out var effectRecord))
        {
            exportName = effectRecord.Name;
        }

        var guid = FSLID.FromNative(kind, nativeId);
        var abilityCategory = inputs.Export.CategoryFor(ability?.Category);
        var icon = inputs.Icons.IconFor(guid) ?? string.Empty;

        var addedGaps = new List<Gap>();
        var costs = ability is not null ? Costs.Map(ability, addedGaps, scope, member) : EmptyCosts;

        var prov = new ProvenanceBuilder()
            .Set("id", ProvenanceSource.Export)
            .Set("kind", ProvenanceSource.Export)
            .SetIf("name", exportName is not null, ProvenanceSource.Export)
            .SetIf("icon", icon.Length > 0, ProvenanceSource.Icons)
            .SetIf("abilityCategory", abilityCategory.HasValue, ProvenanceSource.Export)
            .SetIf("cooldown", ability?.Cooldown is not null, ProvenanceSource.Export)
            .SetIf("range", ability?.Range is not null, ProvenanceSource.Export)
            .SetIf("radius", ability?.Radius is not null, ProvenanceSource.Export)
            .Set("charges", ProvenanceSource.Export)
            .SetIf("castDuration", ability?.CastTime is not null, ProvenanceSource.Export)
            .SetIf("channelDuration", ability?.ChannelTime is not null, ProvenanceSource.Export)
            .SetIf("channelTickInterval", ability?.ChannelTick is not null, ProvenanceSource.Export)
            .SetIf("costs", costs.Count > 0, ProvenanceSource.Export);

        var baseSpell = BuildSpell(kind, nativeId, exportName ?? string.Empty, icon,
            cooldown: ability?.Cooldown,
            cooldownReductionOnTargetDeath: null,
            range: ability?.Range,
            radius: ability?.Radius,
            charges: ability?.ChargeCount ?? 1,
            castDuration: ability?.CastTime,
            channelDuration: ability?.ChannelTime,
            channelTickInterval: ability?.ChannelTick,
            costs: costs,
            abilityCategory: abilityCategory);
        var curated = ApplyPatch(new CuratedSpell(scope, member, baseSpell, prov.Build()), delta);

        if (!MemberNaming.IsValidIdentifier(curated.Member))
            addedGaps.Add(new Gap(scope, curated.Member, GapKind.MissingName));
        if (string.IsNullOrEmpty(curated.Spell.Icon))
            addedGaps.Add(new Gap(scope, curated.Member, GapKind.MissingIcon));

        return (curated, addedGaps);
    }
}
