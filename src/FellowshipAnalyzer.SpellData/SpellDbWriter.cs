using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;


namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// Deterministic System.Text.Json serializer for the committed <c>spelldb.json</c>.
/// Each entry is a polymorphic <see cref="Spell"/> (discriminated by <c>kind</c>). Scopes are
/// written heroes-first (ordinal), then <c>shared</c>, then <c>items</c>, then the hero-independent
/// <c>schools</c> and <c>rarities</c> maps; members ordinal; cost keys ordered by
/// <c>ResourceTypes</c> value. Default <c>charges</c> (1) and empty <c>costs</c> are pruned.
/// Provenance and gaps are not serialized.
/// </summary>
public static class SpellDbWriter
{
    /// <summary>The top-level key holding the FSLID → damage school map, which is not a spell scope.</summary>
    public const string SchoolsSection = "schools";

    /// <summary>The top-level key holding the rarity tier → stored name map, which is not a spell scope.</summary>
    public const string RaritiesSection = "rarities";

    /// <summary>The top-level key holding the per-hero talent registries, which is not a spell scope.</summary>
    public const string TalentsSection = "talents";

    /// <summary>The top-level key holding the per-hero legendary item registries, which is not a spell scope.</summary>
    public const string LegendariesSection = "legendaries";

    /// <summary>The top-level key holding the dungeon id → icon map, which is not a spell scope.</summary>
    public const string DungeonsSection = "dungeons";

    /// <summary>The top-level key holding the augmentation id → type and modifier slot map, which is not a spell scope.</summary>
    public const string AugmentationsSection = "augmentations";

    /// <summary>The top-level key holding the effect id → modifiers, duration and Cooldown Acceleration map, which is not a spell scope.</summary>
    public const string EffectsSection = "effects";

    /// <summary>The top-level key holding the secondary stat → rating curve map, which is not a spell scope.</summary>
    public const string RatingCurvesSection = "ratingCurves";

    /// <summary>
    /// The top-level key listing item and gem textures drawn once for every rarity rung, which is not a
    /// spell scope. Named so it cannot collide with the <c>shared</c> spell scope.
    /// </summary>
    public const string TexturesSharedAcrossRungsSection = "texturesSharedAcrossRungs";

    private static readonly JsonSerializerOptions IndentOptions = new() { WriteIndented = true };

    public static string Serialize(MergeResult result)
    {
        var byScope = result.Spells
            .GroupBy(s => s.Scope)
            .ToDictionary(g => g.Key, g => g.ToList());

        var heroScopes = byScope.Keys
            .Where(k => k != "shared" && k != "items" && k != SchoolsSection && k != RaritiesSection
                        && k != TexturesSharedAcrossRungsSection && k != TalentsSection && k != LegendariesSection
                        && k != DungeonsSection && k != AugmentationsSection && k != EffectsSection
                        && k != RatingCurvesSection)
            .OrderBy(k => k, StringComparer.Ordinal);

        var orderedScopes = heroScopes.AsEnumerable();
        if (byScope.ContainsKey("shared"))
            orderedScopes = orderedScopes.Append("shared");
        if (byScope.ContainsKey("items"))
            orderedScopes = orderedScopes.Append("items");

        var root = new JsonObject();
        foreach (var scope in orderedScopes)
        {
            var scopeObj = new JsonObject();
            foreach (var curated in byScope[scope]
                .Where(s => MemberNaming.IsValidIdentifier(s.Member))
                .OrderBy(s => s.Member, StringComparer.Ordinal))
            {
                scopeObj[curated.Member] = ToEntryNode(curated.Spell);
            }
            root[scope] = scopeObj;
        }

        if (result.Talents.Count > 0)
        {
            var talentsObj = new JsonObject();
            foreach (var hero in result.Talents
                .Where(talent => MemberNaming.IsValidIdentifier(talent.Member))
                .GroupBy(talent => talent.Scope)
                .OrderBy(hero => hero.Key, StringComparer.Ordinal))
            {
                var heroObj = new JsonObject();
                foreach (var talent in hero.OrderBy(talent => talent.Member, StringComparer.Ordinal))
                    heroObj[talent.Member] = ToEntryNode(talent.Spell);
                talentsObj[hero.Key] = heroObj;
            }
            root[TalentsSection] = talentsObj;
        }

        if (result.Legendaries.Count > 0)
        {
            var legendariesObj = new JsonObject();
            foreach (var hero in result.Legendaries
                .Where(legendary => MemberNaming.IsValidIdentifier(legendary.Member))
                .GroupBy(legendary => legendary.Scope)
                .OrderBy(hero => hero.Key, StringComparer.Ordinal))
            {
                var heroObj = new JsonObject();
                foreach (var legendary in hero.OrderBy(legendary => legendary.Member, StringComparer.Ordinal))
                {
                    heroObj[legendary.Member] = new JsonObject
                    {
                        ["itemId"] = legendary.ItemId,
                        ["itemName"] = legendary.ItemName,
                        ["powerId"] = legendary.PowerId,
                        ["powerName"] = legendary.PowerName,
                        ["slot"] = legendary.Slot,
                        ["icon"] = legendary.Icon,
                    };
                }
                legendariesObj[hero.Key] = heroObj;
            }
            root[LegendariesSection] = legendariesObj;
        }

        if (result.Schools.Count > 0)
        {
            var schoolsObj = new JsonObject();
            foreach (var (fslId, school) in result.Schools.OrderBy(s => s.Key))
                schoolsObj[fslId.ToString(CultureInfo.InvariantCulture)] = JsonValue.Create(FormatSchool(school));
            root[SchoolsSection] = schoolsObj;
        }

        if (result.Rarities.Count > 0)
        {
            var raritiesObj = new JsonObject();
            foreach (var (tier, name) in result.Rarities.OrderBy(r => r.Key))
                raritiesObj[tier.ToString(CultureInfo.InvariantCulture)] = JsonValue.Create(name);
            root[RaritiesSection] = raritiesObj;
        }

        if (result.Dungeons.Count > 0)
        {
            var dungeonsObj = new JsonObject();
            foreach (var (id, icon) in result.Dungeons.OrderBy(d => d.Key))
                dungeonsObj[id.ToString(CultureInfo.InvariantCulture)] = JsonValue.Create(icon);
            root[DungeonsSection] = dungeonsObj;
        }

        if (result.Augmentations.Count > 0)
        {
            var augmentationsObj = new JsonObject();
            foreach (var augmentation in result.Augmentations.OrderBy(a => a.Id))
            {
                var entry = new JsonObject { ["type"] = augmentation.Type };
                if (augmentation.Modifier is { } modifier)
                    entry["modifier"] = modifier;
                augmentationsObj[augmentation.Id.ToString(CultureInfo.InvariantCulture)] = entry;
            }
            root[AugmentationsSection] = augmentationsObj;
        }

        if (result.EffectData.Count > 0)
        {
            var effectsObj = new JsonObject();
            foreach (var effect in result.EffectData.OrderBy(e => e.Id))
                effectsObj[effect.Id.ToString(CultureInfo.InvariantCulture)] = ToEffectNode(effect);
            root[EffectsSection] = effectsObj;
        }

        if (result.RatingCurves.Count > 0)
        {
            var curvesObj = new JsonObject();
            foreach (var curve in result.RatingCurves)
                curvesObj[curve.Stat] = ToCurveNode(curve.Curve);
            root[RatingCurvesSection] = curvesObj;
        }

        if (result.TexturesSharedAcrossRungs.Count > 0)
        {
            var textureArray = new JsonArray();
            foreach (var texture in result.TexturesSharedAcrossRungs)
                textureArray.Add(JsonValue.Create(texture));
            root[TexturesSharedAcrossRungsSection] = textureArray;
        }

        return root.ToJsonString(IndentOptions);
    }

    public static MergeResult Deserialize(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var spells = new List<CuratedSpell>();
        var schools = new Dictionary<int, MagicSchool>();
        var rarities = new Dictionary<int, string>();
        var texturesSharedAcrossRungs = new SortedSet<string>(StringComparer.Ordinal);
        var talents = new List<CuratedSpell>();
        var legendaries = new List<CuratedLegendary>();
        var dungeons = new Dictionary<int, string>();
        var augmentations = new List<ExportAugmentation>();
        var effectData = new List<ExportEffectData>();
        var ratingCurves = new List<ExportRatingCurve>();

        foreach (var (scope, scopeNode) in root)
        {
            if (scope == TexturesSharedAcrossRungsSection)
            {
                foreach (var texture in scopeNode?.AsArray() ?? [])
                    if (texture?.GetValue<string>() is { Length: > 0 } name)
                        texturesSharedAcrossRungs.Add(name);
                continue;
            }

            if (scopeNode is not JsonObject scopeObj)
                continue;

            if (scope == SchoolsSection)
            {
                foreach (var (id, schoolNode) in scopeObj)
                    if (int.TryParse(id, out var fslId) && schoolNode?.GetValue<string>() is { } text)
                        schools[fslId] = Schools.Parse(text);
                continue;
            }

            if (scope == RaritiesSection)
            {
                foreach (var (tierText, nameNode) in scopeObj)
                    if (int.TryParse(tierText, out var tier) && nameNode?.GetValue<string>() is { } name)
                        rarities[tier] = name;
                continue;
            }

            if (scope == DungeonsSection)
            {
                foreach (var (idText, iconNode) in scopeObj)
                    if (int.TryParse(idText, out var id) && iconNode?.GetValue<string>() is { } icon)
                        dungeons[id] = icon;
                continue;
            }

            if (scope == AugmentationsSection)
            {
                foreach (var (idText, entryNode) in scopeObj)
                    if (int.TryParse(idText, out var id) && entryNode?["type"]?.GetValue<string>() is { } type)
                        augmentations.Add(new ExportAugmentation(id, type, entryNode["modifier"]?.GetValue<string>()));
                continue;
            }

            if (scope == EffectsSection)
            {
                foreach (var (idText, entryNode) in scopeObj)
                    if (int.TryParse(idText, out var id) && entryNode is JsonObject effectEntry)
                        effectData.Add(FromEffectNode(id, effectEntry));
                continue;
            }

            if (scope == RatingCurvesSection)
            {
                foreach (var (stat, curveNode) in scopeObj)
                    if (curveNode is JsonObject curveEntry)
                        ratingCurves.Add(new ExportRatingCurve(stat, FromCurveNode(curveEntry)));
                continue;
            }

            if (scope == TalentsSection)
            {
                foreach (var (hero, heroNode) in scopeObj)
                {
                    if (heroNode is not JsonObject heroObj)
                        continue;
                    foreach (var (member, memberNode) in heroObj)
                    {
                        if (memberNode is not JsonObject entry)
                            continue;
                        var talent = entry.Deserialize<Spell>(SpellDbJsonOptions.Default)!;
                        talents.Add(new CuratedSpell(hero, member, talent, Provenance.Empty));
                    }
                }
                continue;
            }

            if (scope == LegendariesSection)
            {
                foreach (var (hero, heroNode) in scopeObj)
                {
                    if (heroNode is not JsonObject heroObj)
                        continue;
                    foreach (var (member, memberNode) in heroObj)
                    {
                        if (memberNode is not JsonObject entry)
                            continue;
                        legendaries.Add(new CuratedLegendary(
                            hero,
                            member,
                            entry["itemId"]!.GetValue<int>(),
                            entry["itemName"]?.GetValue<string>() ?? string.Empty,
                            entry["powerId"]!.GetValue<int>(),
                            entry["powerName"]?.GetValue<string>() ?? string.Empty,
                            entry["slot"]?.GetValue<string>() ?? string.Empty,
                            entry["icon"]?.GetValue<string>() ?? string.Empty));
                    }
                }
                continue;
            }

            foreach (var (member, memberNode) in scopeObj)
            {
                if (memberNode is not JsonObject entry)
                    continue;
                var spell = entry.Deserialize<Spell>(SpellDbJsonOptions.Default)!;
                spells.Add(new CuratedSpell(scope, member, spell, Provenance.Empty));
            }
        }

        return new MergeResult(spells, []) { Schools = schools, Rarities = rarities, TexturesSharedAcrossRungs = texturesSharedAcrossRungs, Dungeons = dungeons, Augmentations = augmentations, EffectData = effectData, RatingCurves = ratingCurves, Talents = talents, Legendaries = legendaries };
    }

    /// <summary>
    /// Renders a school the way the committed <c>spelldb.json</c> writes it, so a dual-school entry round-trips
    /// as <c>Magic/Physical</c> rather than as a flags list.
    /// </summary>
    public static string FormatSchool(MagicSchool school) =>
        school == (MagicSchool.Magic | MagicSchool.Physical) ? "Magic/Physical" : school.ToString();

    private static JsonObject ToEffectNode(ExportEffectData effect)
    {
        var node = new JsonObject();
        if (effect.Duration is { } duration)
            node["duration"] = duration;
        if (effect.MaxStacks is { } maxStacks)
            node["maxStacks"] = maxStacks;

        if (effect.Modifiers.Length > 0)
        {
            var modifiers = new JsonArray();
            foreach (var modifier in effect.Modifiers)
            {
                var entry = new JsonObject
                {
                    ["attribute"] = modifier.Attribute.ToString(),
                    ["op"] = modifier.Op.ToString(),
                };
                if (modifier.Value is { } value)
                    entry["value"] = value;
                if (modifier.Ranks is { } ranks)
                {
                    var rankArray = new JsonArray();
                    foreach (var rank in ranks)
                        rankArray.Add(JsonValue.Create(rank));
                    entry["ranks"] = rankArray;
                }
                if (modifier.Coefficient is { } coefficient)
                    entry["coefficient"] = coefficient;
                modifiers.Add(entry);
            }
            node["modifiers"] = modifiers;
        }

        if (effect.CooldownAccelerations.Length > 0)
        {
            var accelerations = new JsonArray();
            foreach (var change in effect.CooldownAccelerations)
                accelerations.Add(new JsonObject { ["ability"] = change.AbilityId, ["value"] = change.Value });
            node["cooldownAccelerations"] = accelerations;
        }

        return node;
    }

    private static ExportEffectData FromEffectNode(int id, JsonObject node)
    {
        var modifiers = new List<AttributeModifier>();
        foreach (var entry in node["modifiers"]?.AsArray() ?? [])
        {
            if (entry is not JsonObject modifier)
                continue;
            modifiers.Add(new AttributeModifier(
                Enum.Parse<StatAttribute>(modifier["attribute"]!.GetValue<string>()),
                Enum.Parse<ModifierOp>(modifier["op"]!.GetValue<string>()),
                modifier["value"]?.GetValue<double>(),
                modifier["ranks"]?.AsArray().Select(rank => rank!.GetValue<double>()).ToArray(),
                modifier["coefficient"]?.GetValue<double>()));
        }

        var accelerations = (node["cooldownAccelerations"]?.AsArray() ?? [])
            .OfType<JsonObject>()
            .Select(change => new CooldownAccelerationChange(change["ability"]!.GetValue<int>(), change["value"]!.GetValue<double>()))
            .ToArray();

        return new ExportEffectData(
            id,
            node["duration"]?.GetValue<double>(),
            node["maxStacks"]?.GetValue<int>(),
            [.. modifiers],
            accelerations);
    }

    private static JsonObject ToCurveNode(RatingCurve curve)
    {
        var brackets = new JsonArray();
        foreach (var (maxBracketValue, penaltyPercentage) in curve.Brackets)
            brackets.Add(new JsonObject { ["maxBracketValue"] = maxBracketValue, ["penaltyPercentage"] = penaltyPercentage });

        return new JsonObject
        {
            ["baseStatMultiplier"] = curve.BaseStatMultiplier,
            ["brackets"] = brackets,
            ["finalPenaltyPercentage"] = curve.FinalPenaltyPercentage,
        };
    }

    private static RatingCurve FromCurveNode(JsonObject node) => new(
        node["baseStatMultiplier"]!.GetValue<double>(),
        [.. (node["brackets"]?.AsArray() ?? []).OfType<JsonObject>()
            .Select(bracket => (bracket["maxBracketValue"]!.GetValue<double>(), bracket["penaltyPercentage"]!.GetValue<double>()))],
        node["finalPenaltyPercentage"]!.GetValue<double>());

    private static JsonObject ToEntryNode(Spell spell)
    {
        var node = JsonSerializer.SerializeToNode(spell, SpellDbJsonOptions.Default)!.AsObject();

        if (node.TryGetPropertyValue("charges", out var charges)
            && charges is JsonValue chargesValue
            && chargesValue.TryGetValue<int>(out var c) && c == 1)
            node.Remove("charges");

        if (node.TryGetPropertyValue("costs", out var costs) && costs is JsonObject costsObj)
        {
            if (costsObj.Count == 0)
                node.Remove("costs");
            else
                node["costs"] = OrderCosts(costsObj);
        }

        return node;
    }

    private static JsonObject OrderCosts(JsonObject costs)
    {
        var ordered = new JsonObject();
        foreach (var pair in costs.OrderBy(kv => CostOrder(kv.Key)))
            ordered[pair.Key] = pair.Value!.DeepClone();
        return ordered;
    }

    private static int CostOrder(string token) =>
        ResourceTypesAliases.TryResolve(token, out var slot) ? (int)slot : int.MaxValue;
}
