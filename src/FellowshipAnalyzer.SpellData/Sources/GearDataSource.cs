using System.Text.Json;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>A weapon or weapon trait entry from <c>gear_data.json</c>.</summary>
public record GearWeapon(string DisplayName, int FslId, string DevName, IReadOnlyDictionary<string, double> Scalars);

/// <summary>Loaded contents of <c>gear_data.json</c>.</summary>
public sealed class GearDataSource
{
    public IReadOnlyList<GearWeapon> Weapons { get; }
    public IReadOnlyList<GearWeapon> WeaponTraits { get; }

    private GearDataSource(List<GearWeapon> weapons, List<GearWeapon> weaponTraits)
    {
        Weapons = weapons;
        WeaponTraits = weaponTraits;
    }

    public static GearDataSource Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        return new GearDataSource(
            ReadWeapons(root.GetProperty("Weapons")),
            ReadWeaponTraits(root.GetProperty("WeaponTraits")));
    }

    private static List<GearWeapon> ReadWeapons(JsonElement section)
    {
        var result = new List<GearWeapon>();
        foreach (var entry in section.EnumerateObject())
        {
            var val = entry.Value;
            var fslId = val.TryGetProperty("FSLID", out var fslProp) ? fslProp.GetInt32() : 0;
            var devName = val.TryGetProperty("DevName", out var devProp) ? devProp.GetString() ?? string.Empty : string.Empty;
            result.Add(new GearWeapon(entry.Name, fslId, devName, ExtractNumericScalars(val, "FSLID", "DevName")));
        }
        return result;
    }

    private static List<GearWeapon> ReadWeaponTraits(JsonElement section)
    {
        var result = new List<GearWeapon>();
        foreach (var entry in section.EnumerateObject())
        {
            var val = entry.Value;
            var displayName = val.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString() ?? entry.Name
                : entry.Name;
            var fslId = val.TryGetProperty("FSLID", out var fslProp) ? fslProp.GetInt32() : 0;
            var devName = val.TryGetProperty("DevName", out var devProp) ? devProp.GetString() ?? string.Empty : string.Empty;
            result.Add(new GearWeapon(displayName, fslId, devName, ExtractNumericScalars(val, "FSLID", "DevName", "Name")));
        }
        return result;
    }

    private static Dictionary<string, double> ExtractNumericScalars(JsonElement obj, params string[] skip)
    {
        var result = new Dictionary<string, double>();
        var skipSet = new HashSet<string>(skip, StringComparer.Ordinal);
        foreach (var prop in obj.EnumerateObject())
        {
            if (skipSet.Contains(prop.Name))
                continue;
            if (prop.Value.ValueKind == JsonValueKind.Number)
                result[prop.Name] = prop.Value.GetDouble();
        }
        return result;
    }
}
