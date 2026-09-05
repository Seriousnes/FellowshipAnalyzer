using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;


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

    /// <summary>
    /// The top-level key listing item and gem art drawn once for every rarity rung, which is not a spell
    /// scope. Named so it cannot collide with the <c>shared</c> spell scope.
    /// </summary>
    public const string ArtSharedAcrossRungsSection = "artSharedAcrossRungs";

    private static readonly JsonSerializerOptions IndentOptions = new() { WriteIndented = true };

    public static string Serialize(MergeResult result)
    {
        var byScope = result.Spells
            .GroupBy(s => s.Scope)
            .ToDictionary(g => g.Key, g => g.ToList());

        var heroScopes = byScope.Keys
            .Where(k => k != "shared" && k != "items" && k != SchoolsSection && k != RaritiesSection
                        && k != ArtSharedAcrossRungsSection && k != TalentsSection)
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

        if (result.ArtSharedAcrossRungs.Count > 0)
        {
            var artArray = new JsonArray();
            foreach (var art in result.ArtSharedAcrossRungs)
                artArray.Add(JsonValue.Create(art));
            root[ArtSharedAcrossRungsSection] = artArray;
        }

        return root.ToJsonString(IndentOptions);
    }

    public static MergeResult Deserialize(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var spells = new List<CuratedSpell>();
        var schools = new Dictionary<int, MagicSchool>();
        var rarities = new Dictionary<int, string>();
        var artSharedAcrossRungs = new SortedSet<string>(StringComparer.Ordinal);
        var talents = new List<CuratedSpell>();

        foreach (var (scope, scopeNode) in root)
        {
            if (scope == ArtSharedAcrossRungsSection)
            {
                foreach (var art in scopeNode?.AsArray() ?? [])
                    if (art?.GetValue<string>() is { Length: > 0 } name)
                        artSharedAcrossRungs.Add(name);
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

            foreach (var (member, memberNode) in scopeObj)
            {
                if (memberNode is not JsonObject entry)
                    continue;
                var spell = entry.Deserialize<Spell>(SpellDbJsonOptions.Default)!;
                spells.Add(new CuratedSpell(scope, member, spell, Provenance.Empty));
            }
        }

        return new MergeResult(spells, []) { Schools = schools, Rarities = rarities, ArtSharedAcrossRungs = artSharedAcrossRungs, Talents = talents };
    }

    /// <summary>
    /// Renders a school the way the committed <c>spelldb.json</c> writes it, so a dual-school entry round-trips
    /// as <c>Magic/Physical</c> rather than as a flags list.
    /// </summary>
    public static string FormatSchool(MagicSchool school) =>
        school == (MagicSchool.Magic | MagicSchool.Physical) ? "Magic/Physical" : school.ToString();

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
