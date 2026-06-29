using System.Text.Json;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>Icon lookups by FSL guid, sourced from <c>abilities.json</c>.</summary>
public sealed class IconSource
{
    private readonly Dictionary<int, string> _icons;

    private IconSource(Dictionary<int, string> icons) => _icons = icons;

    /// <summary>Returns the icon filename for the given FSL guid, or <c>null</c> if not found.</summary>
    public string? IconFor(int fslGuid) => _icons.TryGetValue(fslGuid, out var icon) ? icon : null;

    public static IconSource Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var icons = new Dictionary<int, string>();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("Id", out var idProp))
                continue;
            if (!entry.TryGetProperty("Icon", out var iconProp) || iconProp.ValueKind != JsonValueKind.String)
                continue;

            var id = idProp.GetInt32();
            var icon = iconProp.GetString();
            if (icon is not null)
                icons[id] = icon;
        }
        return new IconSource(icons);
    }
}
