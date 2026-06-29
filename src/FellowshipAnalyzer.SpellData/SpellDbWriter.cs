using System.Text;
using System.Text.Json;
using FellowshipAnalyzer.SpellData.Model;

namespace FellowshipAnalyzer.SpellData;

/// <summary>
/// Deterministic serializer for the committed <c>spelldb.json</c> file.
/// Scopes are written heroes-first (alphabetical), then <c>shared</c>, then <c>items</c>.
/// Members within each scope are sorted ordinally. Provenance and Gaps are not serialized.
/// </summary>
public static class SpellDbWriter
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    /// <summary>
    /// Serializes <paramref name="result"/> to an indented JSON string.
    /// <c>kind</c> is omitted when <see cref="SpellKind.Ability"/> (the default).
    /// <c>charges</c> is omitted when 1 (the default). Absent scalars are omitted.
    /// <c>costs</c> is omitted when empty. Cost keys are sorted ordinally.
    /// </summary>
    public static string Serialize(MergeResult result)
    {
        var byScope = result.Spells
            .GroupBy(s => s.Scope)
            .ToDictionary(g => g.Key, g => g.ToList());

        var heroScopes = byScope.Keys
            .Where(k => k != "shared" && k != "items")
            .Order(StringComparer.Ordinal);

        var orderedScopes = heroScopes.AsEnumerable();
        if (byScope.ContainsKey("shared"))
            orderedScopes = orderedScopes.Append("shared");
        if (byScope.ContainsKey("items"))
            orderedScopes = orderedScopes.Append("items");

        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, WriterOptions);

        writer.WriteStartObject();
        foreach (var scope in orderedScopes)
        {
            writer.WritePropertyName(scope);
            writer.WriteStartObject();

            foreach (var spell in byScope[scope]
                .Where(s => MemberNaming.IsValidIdentifier(s.Member))
                .OrderBy(s => s.Member, StringComparer.Ordinal))
            {
                writer.WritePropertyName(spell.Member);
                writer.WriteStartObject();

                writer.WriteNumber("id", spell.Id);

                if (spell.Kind != SpellKind.Ability)
                    writer.WriteString("kind", spell.Kind.ToString().ToLowerInvariant());

                writer.WriteString("name", spell.Name);
                writer.WriteString("icon", spell.Icon);

                if (spell.Cooldown.HasValue)
                    writer.WriteNumber("cooldown", spell.Cooldown.Value);
                if (spell.Range.HasValue)
                    writer.WriteNumber("range", spell.Range.Value);
                if (spell.Charges != 1)
                    writer.WriteNumber("charges", spell.Charges);
                if (spell.CastDuration.HasValue)
                    writer.WriteNumber("castDuration", spell.CastDuration.Value);
                if (spell.ChannelDuration.HasValue)
                    writer.WriteNumber("channelDuration", spell.ChannelDuration.Value);
                if (spell.ChannelTickInterval.HasValue)
                    writer.WriteNumber("channelTickInterval", spell.ChannelTickInterval.Value);

                if (spell.Costs.Count > 0)
                {
                    writer.WritePropertyName("costs");
                    writer.WriteStartObject();
                    foreach (var (key, value) in spell.Costs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                        writer.WriteNumber(key, value);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Deserializes a <c>spelldb.json</c> string (as produced by <see cref="Serialize"/>) into a
    /// <see cref="MergeResult"/>. Provenance is left empty; Gaps are not stored in the file and are
    /// returned as an empty list.
    /// </summary>
    public static MergeResult Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var spells = new List<MergedSpell>();

        foreach (var scopeProp in doc.RootElement.EnumerateObject())
        {
            var scope = scopeProp.Name;
            foreach (var memberProp in scopeProp.Value.EnumerateObject())
            {
                var member = memberProp.Name;
                var entry = memberProp.Value;

                var id = entry.GetProperty("id").GetInt32();

                var kind = SpellKind.Ability;
                if (entry.TryGetProperty("kind", out var kindEl) && kindEl.ValueKind == JsonValueKind.String)
                    Enum.TryParse(kindEl.GetString(), ignoreCase: true, out kind);

                var name = entry.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                var icon = entry.TryGetProperty("icon", out var iconEl) ? iconEl.GetString() ?? string.Empty : string.Empty;

                double? cooldown = TryGetDouble(entry, "cooldown");
                int? range = TryGetInt(entry, "range");
                int charges = TryGetInt(entry, "charges") ?? 1;
                double? castDuration = TryGetDouble(entry, "castDuration");
                double? channelDuration = TryGetDouble(entry, "channelDuration");
                double? channelTickInterval = TryGetDouble(entry, "channelTickInterval");

                var costs = new Dictionary<string, int>();
                if (entry.TryGetProperty("costs", out var costsProp) && costsProp.ValueKind == JsonValueKind.Object)
                    foreach (var costProp in costsProp.EnumerateObject())
                        costs[costProp.Name] = costProp.Value.GetInt32();

                spells.Add(new MergedSpell(
                    scope, member, id, kind, name, icon,
                    cooldown, range, charges, castDuration, channelDuration, channelTickInterval,
                    costs, new Provenance()));
            }
        }

        return new MergeResult(spells, []);
    }

    private static double? TryGetDouble(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetDouble();
        return null;
    }

    private static int? TryGetInt(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return null;
    }
}
