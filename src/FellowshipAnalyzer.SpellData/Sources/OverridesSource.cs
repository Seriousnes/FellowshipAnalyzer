using System.Text.Json.Nodes;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>
/// Sparse per-spell override deltas, indexed by scope → member name. Each delta is a sparse
/// serialized <c>Spell</c> (only curator-set fields) plus an optional non-<c>Spell</c> <c>note</c>.
/// </summary>
public sealed class OverridesSource
{
    /// <summary>All override deltas, keyed first by scope then by member name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonObject>> ByScopeAndMember { get; }

    private OverridesSource(Dictionary<string, IReadOnlyDictionary<string, JsonObject>> data) =>
        ByScopeAndMember = data;

    /// <summary>Loads overrides from <paramref name="path"/>, returning an empty source if the file does not exist.</summary>
    public static OverridesSource Load(string path)
    {
        if (!File.Exists(path))
            return new OverridesSource(new Dictionary<string, IReadOnlyDictionary<string, JsonObject>>());
        return Parse(JsonNode.Parse(File.ReadAllText(path))!.AsObject());
    }

    /// <summary>Parses an inline JSON string with the same scope→member→delta shape as the file.</summary>
    public static OverridesSource FromInline(string json) =>
        Parse(JsonNode.Parse(json)!.AsObject());

    private static OverridesSource Parse(JsonObject root)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, JsonObject>>();
        foreach (var (scope, scopeNode) in root)
        {
            if (scopeNode is not JsonObject scopeObj)
                continue;
            var members = new Dictionary<string, JsonObject>();
            foreach (var (member, memberNode) in scopeObj)
                if (memberNode is JsonObject delta)
                    members[member] = (JsonObject)delta.DeepClone();
            result[scope] = members;
        }
        return new OverridesSource(result);
    }
}
