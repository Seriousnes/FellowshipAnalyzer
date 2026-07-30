using System.Text.Json;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>A spell or effect entry from <c>spell_data.json</c>.</summary>
/// <param name="FslId">The namespaced FellowshipLogs id.</param>
/// <param name="Name">The display name, or <c>null</c> for a dev-only entry.</param>
/// <param name="DevName">The game's internal name.</param>
/// <param name="Kind">Which section the entry came from.</param>
/// <param name="School">
/// The damage school, or the default when the entry carries no <c>School</c>. The dump writes
/// <c>Magic/Physical</c> for an entry that deals both, which parses to both flags.
/// </param>
public record GlobalSpell(int FslId, string? Name, string DevName, SpellKind Kind, MagicSchool School = default);

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
            var school = val.TryGetProperty("School", out var schoolProp) && schoolProp.ValueKind == JsonValueKind.String
                ? ParseSchool(schoolProp.GetString())
                : default;

            result[nativeId] = new GlobalSpell(fslId, name, devName, kind, school);
        }
        return result;
    }

    /// <summary>
    /// Parses a <c>School</c> string. The dump joins schools with <c>/</c>, so <c>Magic/Physical</c>
    /// yields both flags. An unrecognized token contributes nothing.
    /// </summary>
    public static MagicSchool ParseSchool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return default;

        var school = default(MagicSchool);
        foreach (var part in value.Split('/'))
            if (Enum.TryParse<MagicSchool>(part.Trim(), ignoreCase: true, out var parsed))
                school |= parsed;

        return school;
    }
}
