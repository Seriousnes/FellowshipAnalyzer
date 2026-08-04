using System.Text.Json;

using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.SpellData.Json;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>An ability entry from a hero's Kit.</summary>
public record KitAbility(int FslId, string? Name, string DevName, AbilityCategory? AbilityCategory);

/// <summary>A named constants block from a hero's Constants section.</summary>
public record ConstantsEntry(string Key, string DevName, IReadOnlyDictionary<string, double> Scalars);

/// <summary>Maps each hero resource's <c>CostType</c> to its <see cref="ResourceTypes"/> slot, plus any unresolved names.</summary>
public record ResourceModel(
    IReadOnlyDictionary<string, ResourceTypes> CostTypeToResource,
    IReadOnlyList<string> UnknownResourceNames);

/// <summary>A single hero's data record from <c>hero_data.json</c>.</summary>
public record HeroRecord(
    string DisplayName,
    string DevKey,
    IReadOnlyList<KitAbility> Kit,
    IReadOnlyList<ConstantsEntry> Constants,
    ResourceModel Resources);

/// <summary>Loaded contents of <c>hero_data.json</c>.</summary>
public sealed class HeroDataSource
{
    public IReadOnlyList<HeroRecord> Heroes { get; }

    private HeroDataSource(List<HeroRecord> heroes) => Heroes = heroes;

    public static HeroDataSource Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var heroes = new List<HeroRecord>();
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var val = entry.Value;
            if (val.ValueKind != JsonValueKind.Object)
                continue;
            if (!val.TryGetProperty("DevKey", out var devKeyProp))
                continue;

            var devKey = devKeyProp.GetString() ?? string.Empty;
            var kit = ReadKit(val);
            var constants = ReadConstants(val);
            var resources = ReadResources(val);
            heroes.Add(new HeroRecord(entry.Name, devKey, kit, constants, resources));
        }
        return new HeroDataSource(heroes);
    }

    private static List<KitAbility> ReadKit(JsonElement hero)
    {
        var result = new List<KitAbility>();
        if (!hero.TryGetProperty("Kit", out var kitProp) || kitProp.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var entry in kitProp.EnumerateObject())
        {
            var val = entry.Value;
            if (val.ValueKind != JsonValueKind.Object)
                continue;

            var fslId = val.TryGetProperty("FSLID", out var fslProp) ? fslProp.GetInt32() : 0;
            string? name = null;
            if (val.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                name = nameProp.GetString();
            var devName = val.TryGetProperty("DevName", out var devProp) ? devProp.GetString() ?? string.Empty : string.Empty;
            var abilityCategory = ReadAbilityCategory(val);
            result.Add(new KitAbility(fslId, name, devName, abilityCategory));
        }
        return result;
    }

    private static AbilityCategory? ReadAbilityCategory(JsonElement ability) =>
        ability.TryGetProperty("AbilityCategory", out var prop)
            && prop.ValueKind == JsonValueKind.String
            && Enum.TryParse<AbilityCategory>(prop.GetString(), ignoreCase: true, out var category)
                ? category
                : null;

    private static List<ConstantsEntry> ReadConstants(JsonElement hero)
    {
        var result = new List<ConstantsEntry>();
        if (!hero.TryGetProperty("Constants", out var constProp) || constProp.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var entry in constProp.EnumerateObject())
        {
            var val = entry.Value;
            if (val.ValueKind != JsonValueKind.Object)
                continue;

            var devName = val.TryGetProperty("DevName", out var devProp) ? devProp.GetString() ?? string.Empty : string.Empty;
            var scalars = new Dictionary<string, double>();
            foreach (var prop in val.EnumerateObject())
            {
                if (prop.Name == "DevName")
                    continue;
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    scalars[prop.Name] = prop.Value.GetDouble();
            }
            result.Add(new ConstantsEntry(entry.Name, devName, scalars));
        }
        return result;
    }

    private static ResourceModel ReadResources(JsonElement hero)
    {
        var mapping = new Dictionary<string, ResourceTypes>(StringComparer.Ordinal);
        var unknown = new List<string>();
        if (!hero.TryGetProperty("HeroResources", out var heroRes))
            return new ResourceModel(mapping, unknown);
        if (!heroRes.TryGetProperty("Resources", out var resList) || resList.ValueKind != JsonValueKind.Array)
            return new ResourceModel(mapping, unknown);

        foreach (var res in resList.EnumerateArray())
        {
            if (!res.TryGetProperty("CostType", out var costTypeProp))
                continue;
            var costType = costTypeProp.GetString();
            if (costType is null)
                continue;

            string? name = null;
            if (res.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                name = nameProp.GetString();

            if (ResourceTypesAliases.TryResolve(name, out var slot))
                mapping[costType] = slot;
            else if (name is not null)
                unknown.Add(name);
        }
        return new ResourceModel(mapping, unknown);
    }
}
