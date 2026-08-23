using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.SpellData.Sources;

public record EntityRef(string Type, int Id, string? Name);

public record ExportAbility(
    int Id,
    string? Name,
    string? Category,
    double? Cooldown,
    double? Cost,
    string? Resource,
    bool CostIsFraction,
    double? Range,
    int? Charges,
    double? CastTime,
    double? ChannelTime,
    double? ChannelTick,
    List<string> Schools,
    List<string> Heroes)
{
    public int? RangeYards => Range is { } range ? (int)Math.Round(range / 100) : null;

    public int ChargeCount => Charges ?? 1;
}

public record ExportEffect(
    int Id,
    string? Name,
    EntityRef? PartOf,
    string? Role,
    List<string> Schools,
    List<string> Heroes);

public record ExportHero(string Name, string? ArmorType, string? PrimaryStat, string? Color);

public sealed class ExportSource
{
    public Dictionary<int, ExportAbility> Abilities { get; }

    public Dictionary<int, ExportEffect> Effects { get; }

    public List<ExportHero> Heroes { get; }

    public Dictionary<string, AbilityCategory?> AbilityCategories { get; }

    private ExportSource(
        Dictionary<int, ExportAbility> abilities,
        Dictionary<int, ExportEffect> effects,
        List<ExportHero> heroes,
        Dictionary<string, AbilityCategory?> abilityCategories)
    {
        Abilities = abilities;
        Effects = effects;
        Heroes = heroes;
        AbilityCategories = abilityCategories;
    }

    public AbilityCategory? CategoryFor(string? category)
    {
        if (category is null)
            return null;
        if (AbilityCategories.TryGetValue(category, out var resolved))
            return resolved;
        throw new InvalidOperationException(
            $"The export writes ability category '{category}', which settings.json does not declare.");
    }

    public static ExportSource Load(string entitiesPath, string settingsPath)
    {
        var abilities = new Dictionary<int, ExportAbility>();
        var effects = new Dictionary<int, ExportEffect>();

        foreach (var line in File.ReadLines(entitiesPath))
        {
            if (line.Length == 0)
                continue;

            var record = JsonSerializer.Deserialize<ExportRecord>(line, Options)
                ?? throw new InvalidOperationException($"Could not read a record from '{entitiesPath}'.");

            if (record.Tag is not null)
                throw new InvalidOperationException(
                    $"The export at '{entitiesPath}' still carries internal asset names. Regenerate it.");

            switch (record.Type)
            {
                case "ability":
                    abilities[record.Id] = new ExportAbility(
                        record.Id, record.Name, record.Category, record.Cooldown, record.Cost,
                        record.Resource, record.CostIsFraction, record.Range, record.Charges,
                        record.CastTime, record.ChannelTime, record.ChannelTick,
                        record.Schools ?? [], record.Heroes ?? []);
                    break;
                case "effect":
                    effects[record.Id] = new ExportEffect(
                        record.Id, record.Name, record.PartOf, record.Role,
                        record.Schools ?? [], record.Heroes ?? []);
                    break;
            }
        }

        var settings = JsonSerializer.Deserialize<ExportSettings>(File.ReadAllText(settingsPath), Options)
            ?? throw new InvalidOperationException($"Could not read '{settingsPath}'.");

        var heroes = (settings.Heroes ?? [])
            .Select(hero => new ExportHero(hero.Name, hero.ArmorType, hero.PrimaryStat, hero.Color))
            .ToList();

        var categories = new Dictionary<string, AbilityCategory?>(StringComparer.Ordinal);
        foreach (var category in settings.AbilityCategories ?? [])
        {
            if (category.Name == NoCategory)
                categories[category.Name] = null;
            else if (Enum.TryParse<AbilityCategory>(category.Name, out var parsed))
                categories[category.Name] = parsed;
            else
                throw new InvalidOperationException(
                    $"settings.json declares ability category '{category.Name}', which AbilityCategory does not.");
        }

        return new ExportSource(abilities, effects, heroes, categories);
    }

    private const string NoCategory = "None";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class ExportRecord
    {
        [JsonPropertyName("$type")]
        public string Type { get; init; } = string.Empty;
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? Category { get; init; }
        public double? Cooldown { get; init; }
        public double? Cost { get; init; }
        public string? Resource { get; init; }
        public bool CostIsFraction { get; init; }
        public double? Range { get; init; }
        public int? Charges { get; init; }
        public double? CastTime { get; init; }
        public double? ChannelTime { get; init; }
        public double? ChannelTick { get; init; }
        public List<string>? Schools { get; init; }
        public List<string>? Heroes { get; init; }
        public EntityRef? PartOf { get; init; }
        public string? Role { get; init; }
        public string? Tag { get; init; }
    }

    private sealed class ExportSettings
    {
        public List<SettingsHero>? Heroes { get; init; }
        public List<SettingsAbilityCategory>? AbilityCategories { get; init; }
    }

    private sealed class SettingsHero
    {
        public string Name { get; init; } = string.Empty;
        public string? ArmorType { get; init; }
        public string? PrimaryStat { get; init; }
        public string? Color { get; init; }
    }

    private sealed class SettingsAbilityCategory
    {
        public string Name { get; init; } = string.Empty;
        public int Value { get; init; }
    }
}
