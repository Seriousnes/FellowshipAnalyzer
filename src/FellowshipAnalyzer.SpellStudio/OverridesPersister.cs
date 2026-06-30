using System.Text.Json;
using System.Text.Json.Nodes;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellStudio;

public static class OverridesPersister
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static void Save(string scope, string member, OverrideEntry delta)
    {
        var path = SourcePaths.Overrides;
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

        foreach (var (key, value) in BuildDeltaNode(delta))
            memberObj[key] = value?.DeepClone();

        File.WriteAllText(path, root.ToJsonString(WriteOptions));
    }

    private static JsonObject BuildDeltaNode(OverrideEntry delta)
    {
        var node = new JsonObject();
        if (delta.Id.HasValue) node["id"] = delta.Id.Value;
        if (delta.Kind.HasValue) node["kind"] = delta.Kind.Value.ToString().ToLowerInvariant();
        if (delta.Name is not null) node["name"] = delta.Name;
        if (delta.Icon is not null) node["icon"] = delta.Icon;
        if (delta.Cooldown.HasValue) node["cooldown"] = delta.Cooldown.Value;
        if (delta.Range.HasValue) node["range"] = delta.Range.Value;
        if (delta.Charges.HasValue) node["charges"] = delta.Charges.Value;
        if (delta.CastDuration.HasValue) node["castDuration"] = delta.CastDuration.Value;
        if (delta.ChannelDuration.HasValue) node["channelDuration"] = delta.ChannelDuration.Value;
        if (delta.ChannelTickInterval.HasValue) node["channelTickInterval"] = delta.ChannelTickInterval.Value;
        if (delta.Costs.Count > 0)
        {
            var costsNode = new JsonObject();
            foreach (var (slot, v) in delta.Costs.OrderBy(x => (int)x.Key))
                costsNode[ResourceTypesAliases.ToToken(slot)] = v;
            node["costs"] = costsNode;
        }
        if (delta.Note is not null) node["note"] = delta.Note;
        return node;
    }
}
