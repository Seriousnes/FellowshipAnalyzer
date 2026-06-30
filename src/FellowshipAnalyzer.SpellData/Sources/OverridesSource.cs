using System.Text.Json;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;
using FellowshipAnalyzer.SpellData.Model;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>
/// A per-spell override delta: each nullable field overrides the corresponding normalized
/// <see cref="CuratedSpell"/> property when present.
/// </summary>
public record OverrideEntry(
    int? Id,
    SpellKind? Kind,
    string? Name,
    string? Icon,
    double? Cooldown,
    int? Range,
    int? Charges,
    double? CastDuration,
    double? ChannelDuration,
    double? ChannelTickInterval,
    IReadOnlyDictionary<ResourceTypes, int> Costs,
    string? Note);

/// <summary>Override deltas indexed by scope → member name.</summary>
public sealed class OverridesSource
{
    /// <summary>All override deltas, keyed first by scope then by member name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, OverrideEntry>> ByScopeAndMember { get; }

    private OverridesSource(Dictionary<string, IReadOnlyDictionary<string, OverrideEntry>> data) =>
        ByScopeAndMember = data;

    /// <summary>Loads overrides from <paramref name="path"/>, returning an empty source if the file does not exist.</summary>
    public static OverridesSource Load(string path)
    {
        if (!File.Exists(path))
            return new OverridesSource(new Dictionary<string, IReadOnlyDictionary<string, OverrideEntry>>());
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return Parse(doc.RootElement);
    }

    /// <summary>Parses an inline JSON string with the same camelCase shape as the overrides file.</summary>
    public static OverridesSource FromInline(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Parse(doc.RootElement);
    }

    private static OverridesSource Parse(JsonElement root)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, OverrideEntry>>();
        foreach (var scope in root.EnumerateObject())
        {
            var members = new Dictionary<string, OverrideEntry>();
            foreach (var member in scope.Value.EnumerateObject())
                members[member.Name] = ParseEntry(member.Value);
            result[scope.Name] = members;
        }
        return new OverridesSource(result);
    }

    private static OverrideEntry ParseEntry(JsonElement val)
    {
        int? id = TryGetInt(val, "id");

        SpellKind? kind = null;
        if (val.TryGetProperty("kind", out var kindProp) && kindProp.ValueKind == JsonValueKind.String)
            kind = Enum.TryParse<SpellKind>(kindProp.GetString(), ignoreCase: true, out var k) ? k : null;

        string? name = TryGetString(val, "name");
        string? icon = TryGetString(val, "icon");
        double? cooldown = TryGetDouble(val, "cooldown");
        int? range = TryGetInt(val, "range");
        int? charges = TryGetInt(val, "charges");
        double? castDuration = TryGetDouble(val, "castDuration");
        double? channelDuration = TryGetDouble(val, "channelDuration");
        double? channelTickInterval = TryGetDouble(val, "channelTickInterval");
        string? note = TryGetString(val, "note");

        var costs = new Dictionary<ResourceTypes, int>();
        if (val.TryGetProperty("costs", out var costsProp) && costsProp.ValueKind == JsonValueKind.Object)
            foreach (var p in costsProp.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number
                    && ResourceTypesAliases.TryResolve(p.Name, out var slot))
                    costs[slot] = p.Value.GetInt32();

        return new OverrideEntry(id, kind, name, icon, cooldown, range, charges,
            castDuration, channelDuration, channelTickInterval, costs, note);
    }

    private static string? TryGetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static double? TryGetDouble(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

    private static int? TryGetInt(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
}
