using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// All upstream data sources loaded and ready for <see cref="MergeEngine.Run"/>.
/// Declared as a record so Task 9 can produce patched variants via <c>with { … }</c>.
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
/// Composes the loaded sources into a list of merged, enriched spells for every hero.
/// </summary>
public static class MergeEngine
{
    /// <summary>
    /// For each hero present in <c>hero_data.json</c>, selects their Kit abilities, resolves scalars from Constants entries,
    /// links and includes spawned effects, and enriches each entry with name/kind from
    /// <c>spell_data</c>, icon from <c>abilities.json</c>, and costs from the merged scalar bag.
    /// After auto-selection, applies overrides: patches existing members in place and adds
    /// new members enriched by id. Emits gaps for missing names, icons, and id-less adds.
    /// </summary>
    public static MergeResult Run(MergeInputs inputs)
    {
        var spells = new List<MergedSpell>();
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

                var prov = new Provenance(
                    Id: ProvenanceSource.HeroData,
                    Kind: ProvenanceSource.HeroData,
                    Name: spellDataName is not null ? ProvenanceSource.SpellData : ProvenanceSource.HeroData,
                    Icon: icon.Length > 0 ? ProvenanceSource.Icons : null,
                    Cooldown: cooldown.HasValue ? ProvenanceSource.HeroData : null,
                    Range: range.HasValue ? ProvenanceSource.HeroData : null,
                    Charges: ProvenanceSource.HeroData,
                    CastDuration: castDuration.HasValue ? ProvenanceSource.HeroData : null,
                    ChannelDuration: channelDuration.HasValue ? ProvenanceSource.HeroData : null,
                    ChannelTickInterval: channelTickInterval.HasValue ? ProvenanceSource.HeroData : null,
                    Costs: costs.Count > 0 ? ProvenanceSource.HeroData : null);

                spells.Add(new MergedSpell(
                    scope, member, nativeId, kind, name, icon,
                    cooldown, range, charges, castDuration, channelDuration, channelTickInterval,
                    costs, prov));

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
                    var effectProv = new Provenance(
                        Id: ProvenanceSource.HeroData,
                        Kind: ProvenanceSource.SpellData,
                        Name: effectEntry?.Name is not null ? ProvenanceSource.SpellData : null,
                        Icon: effectIcon.Length > 0 ? ProvenanceSource.Icons : null,
                        Charges: ProvenanceSource.HeroData);

                    spells.Add(new MergedSpell(
                        scope, effectMember, effectId, effectKind, effectName, effectIcon,
                        null, null, 1, null, null, null,
                        new Dictionary<string, int>(), effectProv));

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

    private static MergedSpell ApplyPatch(MergedSpell spell, OverrideEntry delta)
    {
        var prov = spell.Provenance with
        {
            Id = delta.Id.HasValue ? ProvenanceSource.Override : spell.Provenance.Id,
            Kind = delta.Kind.HasValue ? ProvenanceSource.Override : spell.Provenance.Kind,
            Name = delta.Name is not null ? ProvenanceSource.Override : spell.Provenance.Name,
            Icon = delta.Icon is not null ? ProvenanceSource.Override : spell.Provenance.Icon,
            Cooldown = delta.Cooldown.HasValue ? ProvenanceSource.Override : spell.Provenance.Cooldown,
            Range = delta.Range.HasValue ? ProvenanceSource.Override : spell.Provenance.Range,
            Charges = delta.Charges.HasValue ? ProvenanceSource.Override : spell.Provenance.Charges,
            CastDuration = delta.CastDuration.HasValue ? ProvenanceSource.Override : spell.Provenance.CastDuration,
            ChannelDuration = delta.ChannelDuration.HasValue ? ProvenanceSource.Override : spell.Provenance.ChannelDuration,
            ChannelTickInterval = delta.ChannelTickInterval.HasValue ? ProvenanceSource.Override : spell.Provenance.ChannelTickInterval,
            Costs = delta.Costs.Count > 0 ? ProvenanceSource.Override : spell.Provenance.Costs,
        };

        return spell with
        {
            Id = delta.Id ?? spell.Id,
            Kind = delta.Kind ?? spell.Kind,
            Name = delta.Name ?? spell.Name,
            Icon = delta.Icon ?? spell.Icon,
            Cooldown = delta.Cooldown ?? spell.Cooldown,
            Range = delta.Range ?? spell.Range,
            Charges = delta.Charges ?? spell.Charges,
            CastDuration = delta.CastDuration ?? spell.CastDuration,
            ChannelDuration = delta.ChannelDuration ?? spell.ChannelDuration,
            ChannelTickInterval = delta.ChannelTickInterval ?? spell.ChannelTickInterval,
            Costs = delta.Costs.Count > 0 ? delta.Costs : spell.Costs,
            Provenance = prov,
        };
    }

    private static (MergedSpell Spell, IEnumerable<Gap> Gaps) EnrichById(
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
            : Costs.Map(scalars, new ResourceModel(new Dictionary<string, string>()));

        var nameSource = delta.Name is not null ? ProvenanceSource.Override
            : nameFromSpellData is not null ? ProvenanceSource.SpellData
            : gearWeapon is not null ? ProvenanceSource.GearData
            : (ProvenanceSource?)null;

        var prov = new Provenance(
            Id: ProvenanceSource.Override,
            Kind: delta.Kind.HasValue ? ProvenanceSource.Override : ProvenanceSource.SpellData,
            Name: nameSource,
            Icon: delta.Icon is not null ? ProvenanceSource.Override : (icon.Length > 0 ? ProvenanceSource.Icons : null),
            Cooldown: delta.Cooldown.HasValue ? ProvenanceSource.Override : (cooldown.HasValue ? ProvenanceSource.GearData : null),
            Range: delta.Range.HasValue ? ProvenanceSource.Override : (range.HasValue ? ProvenanceSource.GearData : null),
            Charges: delta.Charges.HasValue ? ProvenanceSource.Override : ProvenanceSource.GearData,
            CastDuration: delta.CastDuration.HasValue ? ProvenanceSource.Override : (castDuration.HasValue ? ProvenanceSource.GearData : null),
            ChannelDuration: delta.ChannelDuration.HasValue ? ProvenanceSource.Override : (channelDuration.HasValue ? ProvenanceSource.GearData : null),
            ChannelTickInterval: delta.ChannelTickInterval.HasValue ? ProvenanceSource.Override : (channelTickInterval.HasValue ? ProvenanceSource.GearData : null),
            Costs: delta.Costs.Count > 0 ? ProvenanceSource.Override : (costs.Count > 0 ? ProvenanceSource.GearData : null));

        var spell = new MergedSpell(
            scope, member, nativeId, kind, resolvedName, icon,
            cooldown, range, charges, castDuration, channelDuration, channelTickInterval,
            costs, prov);

        var addedGaps = new List<Gap>();
        if (!MemberNaming.IsValidIdentifier(member))
            addedGaps.Add(new Gap(scope, member, GapKind.MissingName));
        if (string.IsNullOrEmpty(icon))
            addedGaps.Add(new Gap(scope, member, GapKind.MissingIcon));

        return (spell, addedGaps);
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
