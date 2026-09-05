using System.Text.Json;

using Fellowship.SDK.Documents;

using FellowshipAnalyzer.Core.UI;

using EntityTypes = Fellowship.SDK.EntityTypes;
using SettingsFile = Fellowship.SDK.SettingsFile;

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
    int? Range,
    int? Radius,
    int? Charges,
    double? CastTime,
    double? ChannelTime,
    double? ChannelTick,
    List<string> Schools,
    List<string> Heroes)
{
    public int ChargeCount => Charges ?? 1;
}

public record ExportEffect(
    int Id,
    string? Name,
    EntityRef? PartOf,
    string? Role,
    List<string> Schools,
    List<string> Heroes);

/// <summary>
/// One talent, with the hero whose talent tree slots it. The export declares the talent and the
/// slot separately; only the slot names a hero.
/// </summary>
public record ExportTalent(int Id, string? Name, string Hero);

public record ExportHero(string Name, string? ArmorType, string? PrimaryStat, string? Color);

/// <summary>
/// One rarity rung for items and gems. <paramref name="Name"/> is the name the build stores
/// and the name art files are suffixed with; <paramref name="DisplayName"/> is the name the game
/// prints. The two are offset from tier 4 upwards, so a rung printed as <c>Heroic</c> stores
/// <c>Champion</c> and its art ends <c>-champion</c>.
/// </summary>
public record ExportRarity(int Tier, string Name, string DisplayName);

public sealed class ExportSource
{
    public Dictionary<int, ExportAbility> Abilities { get; }

    public Dictionary<int, ExportEffect> Effects { get; }

    public List<ExportTalent> Talents { get; }

    public List<ExportHero> Heroes { get; }

    public List<ExportRarity> Rarities { get; }

    public Dictionary<string, AbilityCategory?> AbilityCategories { get; }

    private ExportSource(
        Dictionary<int, ExportAbility> abilities,
        Dictionary<int, ExportEffect> effects,
        List<ExportTalent> talents,
        List<ExportHero> heroes,
        List<ExportRarity> rarities,
        Dictionary<string, AbilityCategory?> abilityCategories)
    {
        Abilities = abilities;
        Effects = effects;
        Talents = talents;
        Heroes = heroes;
        Rarities = rarities;
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
        var talentDocuments = new Dictionary<int, TalentDocument>();
        var talentSlots = new List<TalentSlotDocument>();

        foreach (var line in File.ReadLines(entitiesPath))
        {
            if (line.Length == 0)
                continue;

            using var parsed = JsonDocument.Parse(line);
            var root = parsed.RootElement;

            if (root.TryGetProperty("tag", out _))
                throw new InvalidOperationException(
                    $"The export at '{entitiesPath}' still names internal assets. Regenerate it.");

            switch (root.Deserialize<EntityDocument>(EntityDocuments.Options))
            {
                case AbilityDocument ability:
                    abilities[ability.Id] = new ExportAbility(
                        ability.Id, ability.Name, ability.Category?.ToString(), ability.Cooldown, ability.Cost,
                        ability.Resource, CostIsFraction(root), Whole(ability.Range), Whole(ability.Radius), Whole(ability.Charges),
                        ability.CastTime, ability.ChannelTime, ability.ChannelTick,
                        [.. ability.Schools ?? []], [.. (ability.Heroes ?? []).Select(hero => hero.ToString())]);
                    break;
                case EffectDocument effect:
                    effects[effect.Id] = new ExportEffect(
                        effect.Id, effect.Name, Reference(effect.PartOf), effect.Role,
                        [.. effect.Schools ?? []], [.. (effect.Heroes ?? []).Select(hero => hero.ToString())]);
                    break;
                case TalentDocument talent:
                    talentDocuments[talent.Id] = talent;
                    break;
                case TalentSlotDocument slot:
                    talentSlots.Add(slot);
                    break;
            }
        }

        var talents = talentSlots
            .Where(slot => slot.Talent is not null && talentDocuments.ContainsKey(slot.Talent.Id))
            .Select(slot => new ExportTalent(
                slot.Talent!.Id,
                talentDocuments[slot.Talent.Id].Name,
                slot.Hero.ToString()))
            .OrderBy(talent => talent.Hero, StringComparer.Ordinal)
            .ThenBy(talent => talent.Id)
            .ToList();

        var settings = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(settingsPath), EntityDocuments.Options)
            ?? throw new InvalidOperationException($"Could not read '{settingsPath}'.");

        var heroes = (settings.Heroes ?? [])
            .Select(hero => new ExportHero(hero.Name, hero.ArmorType, hero.PrimaryStat, hero.Color))
            .ToList();

        var rarities = (settings.Rarities ?? [])
            .Select(rarity => new ExportRarity(rarity.Tier, rarity.Name, rarity.DisplayName))
            .OrderBy(rarity => rarity.Tier)
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

        return new ExportSource(abilities, effects, talents, heroes, rarities, categories);
    }

    private const string NoCategory = "None";

    private static EntityRef? Reference(Fellowship.SDK.Models.EntityReference? reference) =>
        reference is null ? null : new EntityRef(EntityTypes.Slug(reference.Type), reference.Id, reference.Name);

    private static int? Whole(double? value) => value is { } number ? (int)number : null;

    private static bool CostIsFraction(JsonElement record) =>
        record.TryGetProperty("costIsFraction", out var fraction) && fraction.ValueKind == JsonValueKind.True;
}
