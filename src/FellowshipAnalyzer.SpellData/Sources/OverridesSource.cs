using System.Text.Json;
using FellowshipAnalyzer.SpellData.Model;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>A per-spell override delta from <c>data/overrides.json</c>.</summary>
public record OverrideEntry(
    int? Id,
    SpellKind? Kind,
    string? Name,
    string? Icon,
    IReadOnlyDictionary<string, double> Scalars,
    IReadOnlyDictionary<string, double> Costs,
    string? Note);

/// <summary>Loaded contents of <c>data/overrides.json</c>, indexed by scope → member.</summary>
public sealed class OverridesSource
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, OverrideEntry>> ByScopeAndMember { get; }

    private OverridesSource(Dictionary<string, IReadOnlyDictionary<string, OverrideEntry>> data) =>
        ByScopeAndMember = data;

    public static OverridesSource Load(string path)
    {
        if (!File.Exists(path))
            return new OverridesSource(new Dictionary<string, IReadOnlyDictionary<string, OverrideEntry>>());

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<string, IReadOnlyDictionary<string, OverrideEntry>>();
        foreach (var scope in doc.RootElement.EnumerateObject())
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
        int? id = val.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
            ? idProp.GetInt32()
            : null;

        SpellKind? kind = null;
        if (val.TryGetProperty("Kind", out var kindProp) && kindProp.ValueKind == JsonValueKind.String)
            kind = Enum.TryParse<SpellKind>(kindProp.GetString(), out var k) ? k : null;

        string? name = val.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
            ? nameProp.GetString()
            : null;

        string? icon = val.TryGetProperty("Icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String
            ? iconProp.GetString()
            : null;

        string? note = val.TryGetProperty("Note", out var noteProp) && noteProp.ValueKind == JsonValueKind.String
            ? noteProp.GetString()
            : null;

        var scalars = new Dictionary<string, double>();
        if (val.TryGetProperty("Scalars", out var scalarsProp) && scalarsProp.ValueKind == JsonValueKind.Object)
            foreach (var p in scalarsProp.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number)
                    scalars[p.Name] = p.Value.GetDouble();

        var costs = new Dictionary<string, double>();
        if (val.TryGetProperty("Costs", out var costsProp) && costsProp.ValueKind == JsonValueKind.Object)
            foreach (var p in costsProp.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number)
                    costs[p.Name] = p.Value.GetDouble();

        return new OverrideEntry(id, kind, name, icon, scalars, costs, note);
    }
}
