using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// Deterministic System.Text.Json serializer for the committed <c>spelldb.json</c>.
/// Each entry is a polymorphic <see cref="Spell"/> (discriminated by <c>kind</c>). Scopes are
/// written heroes-first (ordinal), then <c>shared</c>, then <c>items</c>; members ordinal;
/// cost keys ordered by <c>ResourceTypes</c> value. Default <c>charges</c> (1) and empty
/// <c>costs</c> are pruned. Provenance and gaps are not serialized.
/// </summary>
public static class SpellDbWriter
{
    private static readonly JsonSerializerOptions IndentOptions = new() { WriteIndented = true };

    public static string Serialize(MergeResult result)
    {
        var byScope = result.Spells
            .GroupBy(s => s.Scope)
            .ToDictionary(g => g.Key, g => g.ToList());

        var heroScopes = byScope.Keys
            .Where(k => k != "shared" && k != "items")
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

        return root.ToJsonString(IndentOptions);
    }

    public static MergeResult Deserialize(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var spells = new List<CuratedSpell>();

        foreach (var (scope, scopeNode) in root)
        {
            if (scopeNode is not JsonObject scopeObj)
                continue;
            foreach (var (member, memberNode) in scopeObj)
            {
                if (memberNode is not JsonObject entry)
                    continue;
                var spell = entry.Deserialize<Spell>(SpellDbJsonOptions.Default)!;
                spells.Add(new CuratedSpell(scope, member, spell, Provenance.Empty));
            }
        }

        return new MergeResult(spells, []);
    }

    private static JsonNode ToEntryNode(Spell spell)
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
