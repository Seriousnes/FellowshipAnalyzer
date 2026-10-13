using System.Text.Json.Nodes;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>Every spell, effect, talent, item and legendary name in <c>data/spelldb.json</c>.</summary>
public static class SpellNames
{
    private static readonly string[] NameMembers = ["name", "itemName", "powerName"];

    /// <summary>Every name, compared ordinally.</summary>
    public static IReadOnlySet<string> All { get; } = Load();

    private static HashSet<string> Load()
    {
        using var stream = typeof(SpellNames).Assembly.GetManifestResourceStream("spelldb.json")!;
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(JsonNode.Parse(stream), names);
        return names;
    }

    private static void Collect(JsonNode? node, HashSet<string> names)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (NameMembers.Contains(key) && value is JsonValue scalar && scalar.TryGetValue<string>(out var name) && name.Length > 0)
                        names.Add(name);
                    else
                        Collect(value, names);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    Collect(item, names);
                break;
        }
    }
}
