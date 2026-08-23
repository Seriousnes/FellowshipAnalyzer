using System.Text.Json;
using System.Text.Json.Nodes;

namespace FellowshipAnalyzer.SpellStudio;

/// <summary>
/// Persists a sparse override delta (a serialized partial <c>Spell</c>)
/// into <c>overrides.json</c>, merging field-wise into any existing member object.
/// </summary>
public static class OverridesPersister
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Merges <paramref name="delta"/> into the member object at <paramref name="scope"/>/<paramref name="member"/>,
    /// creating it when absent. A <c>null</c> delta value removes that key; a member or scope left empty is pruned.
    /// </summary>
    public static void Save(string scope, string member, JsonObject delta)
    {
        var root = Load();

        if (!root.TryGetPropertyValue(scope, out var scopeNode) || scopeNode is not JsonObject scopeObj)
        {
            scopeObj = [];
            root[scope] = scopeObj;
        }

        if (!scopeObj.TryGetPropertyValue(member, out var memberNode) || memberNode is not JsonObject memberObj)
        {
            memberObj = [];
            scopeObj[member] = memberObj;
        }

        foreach (var (key, value) in delta)
        {
            if (value is null)
                memberObj.Remove(key);
            else
                memberObj[key] = value.DeepClone();
        }

        Prune(root, scope, member);
        Write(root);
    }

    /// <summary>Deletes the whole member entry at <paramref name="scope"/>/<paramref name="member"/>, pruning an emptied scope.</summary>
    public static void Remove(string scope, string member)
    {
        var root = Load();
        if (root.TryGetPropertyValue(scope, out var scopeNode) && scopeNode is JsonObject scopeObj)
        {
            scopeObj.Remove(member);
            if (scopeObj.Count == 0)
                root.Remove(scope);
        }
        Write(root);
    }

    private static JsonObject Load() =>
        File.Exists(SpellData.SourcePaths.Overrides)
            ? JsonNode.Parse(File.ReadAllText(SpellData.SourcePaths.Overrides))!.AsObject()
            : [];

    private static void Prune(JsonObject root, string scope, string member)
    {
        if (root[scope] is not JsonObject scopeObj)
            return;
        if (scopeObj[member] is JsonObject memberObj && memberObj.Count == 0)
            scopeObj.Remove(member);
        if (scopeObj.Count == 0)
            root.Remove(scope);
    }

    private static void Write(JsonObject root) =>
        File.WriteAllText(SpellData.SourcePaths.Overrides, root.ToJsonString(WriteOptions));
}
