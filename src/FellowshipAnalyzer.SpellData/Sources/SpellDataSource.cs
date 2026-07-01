using System.Text.Json;
using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>A spell or effect entry from <c>spell_data.json</c>.</summary>
public record GlobalSpell(int FslId, string? Name, string DevName, SpellKind Kind);

/// <summary>Loaded contents of <c>spell_data.json</c>, keyed by native id.</summary>
public sealed class SpellDataSource
{
    public IReadOnlyDictionary<int, GlobalSpell> Abilities { get; }
    public IReadOnlyDictionary<int, GlobalSpell> Effects { get; }

    private SpellDataSource(Dictionary<int, GlobalSpell> abilities, Dictionary<int, GlobalSpell> effects)
    {
        Abilities = abilities;
        Effects = effects;
    }

    public static SpellDataSource Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        return new SpellDataSource(
            ReadSection(root.GetProperty("Abilities"), SpellKind.Ability),
            ReadSection(root.GetProperty("Effects"), SpellKind.Effect));
    }

    private static Dictionary<int, GlobalSpell> ReadSection(JsonElement section, SpellKind kind)
    {
        var result = new Dictionary<int, GlobalSpell>();
        foreach (var entry in section.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, out var nativeId))
                continue;

            var val = entry.Value;
            var fslId = val.TryGetProperty("FSLID", out var fslProp) ? fslProp.GetInt32() : 0;
            string? name = null;
            if (val.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                name = nameProp.GetString();
            var devName = val.TryGetProperty("DevName", out var devProp) ? devProp.GetString() ?? string.Empty : string.Empty;

            result[nativeId] = new GlobalSpell(fslId, name, devName, kind);
        }
        return result;
    }
}
