using System.Text.Json;
using System.Text.Json.Nodes;

namespace FellowshipAnalyzer.SpellStudio;

/// <summary>
/// Persists a sparse override delta (a serialized partial <c>Spell</c> plus optional <c>note</c>)
/// into <c>overrides.json</c>, merging field-wise into any existing member object.
/// </summary>
public static class OverridesPersister
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static void Save(string scope, string member, JsonObject delta)
    {
        var path = SpellData.SourcePaths.Overrides;
        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))!.AsObject()
            : new JsonObject();

        if (!root.TryGetPropertyValue(scope, out var scopeNode) || scopeNode is not JsonObject scopeObj)
        {
            scopeObj = new JsonObject();
            root[scope] = scopeObj;
        }

        if (!scopeObj.TryGetPropertyValue(member, out var memberNode) || memberNode is not JsonObject memberObj)
        {
            memberObj = new JsonObject();
            scopeObj[member] = memberObj;
        }

        foreach (var (key, value) in delta)
            memberObj[key] = value?.DeepClone();

        File.WriteAllText(path, root.ToJsonString(WriteOptions));
    }
}
