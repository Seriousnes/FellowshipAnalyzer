using System.Text.Json;

using Fellowship.SDK.Documents;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
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
    CooldownScaling? CooldownScaling,
    double? Cost,
    string? Resource,
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

    /// <summary>True when Haste shortens the cast and channel time.</summary>
    public bool CastTimeScalesWithHaste { get; init; }

    /// <summary>The global cooldown in seconds; <c>null</c> when the ability starts none.</summary>
    public double? GlobalCooldown { get; init; }

    /// <summary>True when Haste shortens the global cooldown; <c>null</c> when the export states no global cooldown.</summary>
    public bool? GlobalCooldownScalesWithHaste { get; init; }

    /// <summary>
    /// The talents and legendary powers that raise the ability above one charge onto a charge effect with
    /// Haste in its time rate.
    /// </summary>
    public List<EntityRef> HasteGrantedBy { get; init; } = [];
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

/// <summary>One legendary item, the power it grants, and the hero whose kit it belongs to.</summary>
public record ExportLegendary(int ItemId, string ItemName, int PowerId, string PowerName, string Slot, string Icon, string Hero);

public record ExportHero(string Name, string? ArmorType, string? PrimaryStat, string? Color);

/// <summary>
/// One rarity rung for items and gems. <paramref name="Name"/> is the name the build stores
/// and the name texture files are suffixed with; <paramref name="DisplayName"/> is the name the game
/// prints. The two are offset from tier 4 upwards, so a rung printed as <c>Heroic</c> stores
/// <c>Champion</c> and its texture ends <c>-champion</c>.
/// </summary>
public record ExportRarity(int Tier, string Name, string DisplayName);

/// <summary>
/// One named dungeon, with the texture the codex draws for it. Fellowship Logs indexes its zone
/// encounters by the same id, so a report's zone resolves its own icon. The export states loading
/// screens as dungeons too, which it leaves unnamed, so a name is what marks a dungeon players enter.
/// </summary>
public record ExportDungeon(int Id, string Icon);

/// <summary>
/// One augmentation, with its entity type and the item modifier slot that holds it. Fellowship Logs
/// writes an item's augmentations in one list, so the type is what tells a sigil from an aspect. An
/// augmentation held only in an attunement tree has no <paramref name="Modifier"/>.
/// </summary>
public record ExportAugmentation(int Id, string Type, string? Modifier);

/// <summary>
/// One effect's Haste, Haste rating, Cooldown Recovery and Cooldown Reduction modifiers, its duration, its
/// maximum stacks, and the Cooldown Acceleration it gives named abilities. <paramref name="Id"/> is the
/// native effect id.
/// </summary>
public record ExportEffectData(
    int Id,
    double? Duration,
    int? MaxStacks,
    AttributeModifier[] Modifiers,
    CooldownAccelerationChange[] CooldownAccelerations);

/// <summary>
/// The rating curve of one secondary stat. <paramref name="Stat"/> is the key <c>settings.json</c> gives
/// the stat: <c>CritChance</c>, <c>Haste</c>, <c>Expertise</c> or <c>Spirit</c>.
/// </summary>
public record ExportRatingCurve(string Stat, RatingCurve Curve);

/// <summary>
/// One numeric <c>tuning</c> row of an export document: <paramref name="Number"/> when the row states one
/// number, <paramref name="Numbers"/> when it states one number per rank. Exactly one of the two is set.
/// </summary>
public record ExportTuningValue(double? Number, double[]? Numbers);

/// <summary>One named glyph, the owner of the <c>tuning</c> rows a glyph states.</summary>
public record ExportGlyph(int Id, string Name);

public sealed class ExportSource
{
    public Dictionary<int, ExportAbility> Abilities { get; }

    public Dictionary<int, ExportEffect> Effects { get; }

    public List<ExportTalent> Talents { get; }

    /// <summary>Every legendary item in the export, one entry per hero named in its heroes array, ordered by hero then item id.</summary>
    public List<ExportLegendary> Legendaries { get; }

    public List<ExportHero> Heroes { get; }

    public List<ExportRarity> Rarities { get; }

    public List<ExportDungeon> Dungeons { get; }

    /// <summary>Every augmentation in the export, ordered by id.</summary>
    public List<ExportAugmentation> Augmentations { get; }

    /// <summary>Every effect with a Haste, Haste rating, Cooldown Recovery or Cooldown Reduction modifier or a Cooldown Acceleration change, ordered by id.</summary>
    public List<ExportEffectData> EffectData { get; }

    /// <summary>The rating curves of Critical Strike, Haste, Expertise and Spirit, in that order.</summary>
    public List<ExportRatingCurve> RatingCurves { get; }

    public Dictionary<string, AbilityCategory?> AbilityCategories { get; }

    /// <summary>The description of every talent and legendary power, keyed by its type slug and id.</summary>
    public Dictionary<(string Type, int Id), string> GrantDescriptions { get; private init; } = [];

    /// <summary>Every named glyph in the export, ordered by id.</summary>
    public List<ExportGlyph> Glyphs { get; private init; } = [];

    /// <summary>
    /// The numeric <c>tuning</c> rows of every ability, effect, talent, legendary power and glyph, keyed by
    /// the entity's type slug and native id and ordered ordinally by row key. A row that states no number
    /// is absent.
    /// </summary>
    public Dictionary<(string Type, int Id), SortedDictionary<string, ExportTuningValue>> Tuning { get; private init; } = [];

    private ExportSource(
        Dictionary<int, ExportAbility> abilities,
        Dictionary<int, ExportEffect> effects,
        List<ExportTalent> talents,
        List<ExportLegendary> legendaries,
        List<ExportHero> heroes,
        List<ExportRarity> rarities,
        List<ExportDungeon> dungeons,
        List<ExportAugmentation> augmentations,
        List<ExportEffectData> effectData,
        List<ExportRatingCurve> ratingCurves,
        Dictionary<string, AbilityCategory?> abilityCategories)
    {
        Abilities = abilities;
        Effects = effects;
        Talents = talents;
        Legendaries = legendaries;
        Heroes = heroes;
        Rarities = rarities;
        Dungeons = dungeons;
        Augmentations = augmentations;
        EffectData = effectData;
        RatingCurves = ratingCurves;
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
        var legendaries = new List<ExportLegendary>();
        var dungeons = new List<ExportDungeon>();
        var augmentationDocuments = new List<AugmentationDocument>();
        var effectData = new Dictionary<int, ExportEffectData>();
        var grantDescriptions = new Dictionary<(string Type, int Id), string>();
        var glyphs = new List<ExportGlyph>();
        var tuning = new Dictionary<(string Type, int Id), SortedDictionary<string, ExportTuningValue>>();

        foreach (var line in File.ReadLines(entitiesPath))
        {
            if (line.Length == 0)
                continue;

            switch (JsonSerializer.Deserialize<EntityDocument>(line, EntityDocuments.Options))
            {
                case AbilityDocument ability:
                    abilities[ability.Id] = new ExportAbility(
                        ability.Id, ability.Name, ability.Category?.ToString(), ability.Cooldown,
                        Scaling(ability), ability.Cost,
                        ability.Resource, Whole(ability.Range), Whole(ability.Radius), Whole(ability.Charges),
                        ability.CastTime, ability.ChannelTime, ability.ChannelTick,
                        [.. ability.Schools ?? []], [.. (ability.Heroes ?? []).Select(hero => hero.ToString())])
                    {
                        CastTimeScalesWithHaste = ability.CastTimeScalesWithHaste == true,
                        GlobalCooldown = ability.GlobalCooldown,
                        GlobalCooldownScalesWithHaste = ability.GlobalCooldownScalesWithHaste,
                        HasteGrantedBy = [.. (ability.CooldownHasteGrantedBy ?? []).Select(granter => Reference(granter)!)],
                    };
                    AddTuning(tuning, Fellowship.SDK.EntityType.Ability, ability.Id, ability.Tuning);
                    break;
                case EffectDocument effect:
                    effects[effect.Id] = new ExportEffect(
                        effect.Id, effect.Name, Reference(effect.PartOf), effect.Role,
                        [.. effect.Schools ?? []], [.. (effect.Heroes ?? []).Select(hero => hero.ToString())]);
                    if (EffectDataOf(effect) is { } data)
                        effectData[effect.Id] = data;
                    AddTuning(tuning, Fellowship.SDK.EntityType.Effect, effect.Id, effect.Tuning);
                    break;
                case TalentDocument talent:
                    talentDocuments[talent.Id] = talent;
                    if (talent.Description is { } talentText)
                        grantDescriptions[(EntityTypes.Slug(Fellowship.SDK.EntityType.Talent), talent.Id)] = talentText;
                    AddTuning(tuning, Fellowship.SDK.EntityType.Talent, talent.Id, talent.Tuning);
                    break;
                case LegendaryPowerDocument power:
                    if (power.Description is { } powerText)
                        grantDescriptions[(EntityTypes.Slug(Fellowship.SDK.EntityType.LegendaryPower), power.Id)] = powerText;
                    AddTuning(tuning, Fellowship.SDK.EntityType.LegendaryPower, power.Id, power.Tuning);
                    break;
                case GlyphDocument glyph:
                    if (!string.IsNullOrEmpty(glyph.Name))
                        glyphs.Add(new ExportGlyph(glyph.Id, glyph.Name));
                    AddTuning(tuning, Fellowship.SDK.EntityType.Glyph, glyph.Id, glyph.Tuning);
                    break;
                case TalentSlotDocument slot:
                    talentSlots.Add(slot);
                    break;
                case ItemDocument item
                    when item.Grants is { } grants && EntityTypes.Slug(grants.Type) == "legendary-power":
                    foreach (var hero in item.Heroes ?? [])
                    {
                        legendaries.Add(new ExportLegendary(
                            item.Id,
                            item.Name ?? string.Empty,
                            grants.Id,
                            grants.Name ?? string.Empty,
                            item.Slot ?? string.Empty,
                            item.Icon ?? string.Empty,
                            hero.ToString()));
                    }
                    break;
                case DungeonDocument dungeon
                    when !string.IsNullOrEmpty(dungeon.Icon) && !string.IsNullOrEmpty(dungeon.Name):
                    dungeons.Add(new ExportDungeon(dungeon.Id, dungeon.Icon));
                    break;
                case AugmentationDocument augmentation:
                    augmentationDocuments.Add(augmentation);
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

        dungeons.Sort((left, right) => left.Id.CompareTo(right.Id));
        legendaries.Sort((left, right) => string.CompareOrdinal(left.Hero, right.Hero) is var byHero && byHero != 0 ? byHero : left.ItemId.CompareTo(right.ItemId));

        return new ExportSource(
            abilities, effects, talents, legendaries, heroes, rarities, dungeons,
            BuildAugmentations(augmentationDocuments, settings),
            [.. effectData.Values.OrderBy(data => data.Id)], BuildRatingCurves(settings), categories)
        {
            GrantDescriptions = grantDescriptions,
            Glyphs = [.. glyphs.OrderBy(glyph => glyph.Id)],
            Tuning = tuning,
        };
    }

    private static void AddTuning(
        Dictionary<(string Type, int Id), SortedDictionary<string, ExportTuningValue>> tuning,
        Fellowship.SDK.EntityType type,
        int id,
        IReadOnlyDictionary<string, Fellowship.SDK.Models.TuningReference>? rows)
    {
        if (rows is null)
            return;

        SortedDictionary<string, ExportTuningValue>? numeric = null;
        foreach (var (key, row) in rows)
        {
            if (TuningValueOf(row.Value) is not { } value)
                continue;

            numeric ??= new SortedDictionary<string, ExportTuningValue>(StringComparer.Ordinal);
            numeric[key] = value;
        }

        if (numeric is not null)
            tuning[(EntityTypes.Slug(type), id)] = numeric;
    }

    private static ExportTuningValue? TuningValueOf(object? value)
    {
        if (value is not JsonElement element)
            return null;

        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new ExportTuningValue(element.GetDouble(), null);
            case JsonValueKind.Array:
                var numbers = new double[element.GetArrayLength()];
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Number)
                        return null;
                    numbers[index++] = item.GetDouble();
                }

                return numbers.Length == 0 ? null : new ExportTuningValue(null, numbers);
            default:
                return null;
        }
    }

    private static List<ExportAugmentation> BuildAugmentations(List<AugmentationDocument> documents, SettingsFile settings)
    {
        var augmentations = new Dictionary<int, ExportAugmentation>();

        foreach (var document in documents)
        {
            var type = EntityTypes.TypeOf(document).ToString();
            var modifier = document.CategoryTag is { } tag
                ? settings.StatCalculation.SlotDefinitions
                    .Where(slot => slot.Value.AugmentationPool.Contains(tag, StringComparer.Ordinal))
                    .Select(slot => slot.Key)
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault()
                : null;

            if (augmentations.TryGetValue(document.Id, out var known) && known.Type != type)
                throw new InvalidOperationException(
                    $"The export writes augmentation id {document.Id} as both {known.Type} and {type}.");

            augmentations[document.Id] = new ExportAugmentation(document.Id, type, modifier);
        }

        return [.. augmentations.Values.OrderBy(augmentation => augmentation.Id)];
    }

    private static ExportEffectData? EffectDataOf(EffectDocument effect)
    {
        var modifiers = new List<AttributeModifier>();
        foreach (var modifier in effect.Modifiers ?? [])
        {
            if (!TryParseAttribute(modifier.Attribute, out var attribute))
                continue;

            var op = modifier.Op switch
            {
                nameof(ModifierOp.Additive) => ModifierOp.Additive,
                nameof(ModifierOp.Multiplicitive) => ModifierOp.Multiplicitive,
                nameof(ModifierOp.MultiplyCompound) => ModifierOp.MultiplyCompound,
                nameof(ModifierOp.AddFinal) => ModifierOp.AddFinal,
                _ => throw new InvalidOperationException(
                    $"Effect {effect.Id} modifies {attribute} with op '{modifier.Op}', which the effect data does not model."),
            };

            modifiers.Add(new AttributeModifier(
                attribute, op, modifier.Value,
                modifier.Ranks is { Count: > 0 } ranks ? [.. ranks] : null,
                modifier.Coefficient));
        }

        var accelerations = (effect.Changes ?? [])
            .Where(change => change.Field == CooldownAccelerationField
                             && change.Target is { } target
                             && EntityTypes.Slug(target.Type) == AbilitySlug)
            .Select(change => new CooldownAccelerationChange(change.Target.Id, change.Value))
            .ToArray();

        if (modifiers.Count == 0 && accelerations.Length == 0)
            return null;

        return new ExportEffectData(effect.Id, effect.Duration, effect.MaxStacks, [.. modifiers], accelerations);
    }

    private static bool TryParseAttribute(string? text, out StatAttribute attribute)
    {
        switch (text)
        {
            case nameof(StatAttribute.Haste):
                attribute = StatAttribute.Haste;
                return true;
            case nameof(StatAttribute.HasteRating):
                attribute = StatAttribute.HasteRating;
                return true;
            case nameof(StatAttribute.CooldownRecovery):
                attribute = StatAttribute.CooldownRecovery;
                return true;
            case nameof(StatAttribute.CooldownReduction):
                attribute = StatAttribute.CooldownReduction;
                return true;
            default:
                attribute = default;
                return false;
        }
    }

    private static List<ExportRatingCurve> BuildRatingCurves(SettingsFile settings)
    {
        var attributes = settings.StatCalculation.Attributes;
        var curves = new List<ExportRatingCurve>();

        foreach (var stat in RatingCurveStats)
        {
            if (!attributes.TryGetValue(stat, out var display))
                throw new InvalidOperationException(
                    $"settings.json declares no statCalculation attribute '{stat}'.");

            curves.Add(new ExportRatingCurve(stat, new RatingCurve(
                display.BaseStatMultiplier,
                [.. (display.Brackets ?? []).Select(bracket => (bracket.MaxBracketValue, bracket.PenaltyPercentage))],
                display.FinalPenaltyPercentage)));
        }

        return curves;
    }

    private static readonly string[] RatingCurveStats = ["CritChance", "Haste", "Expertise", "Spirit"];

    private const string CooldownAccelerationField = "cooldownAcceleration";

    private const string AbilitySlug = "ability";

    private const string NoCategory = "None";

    private const string HasteAttribute = "Haste";

    private const string CooldownReductionAttribute = "CooldownReduction";

    private const string CooldownRecoveryAttribute = "CooldownRecovery";

    private static CooldownScaling? Scaling(AbilityDocument ability)
    {
        var duration = ability.CooldownDurationScalesOff ?? [];
        var timeRate = ability.CooldownTimeRateScalesOff ?? [];
        if (duration.Count == 0 && timeRate.Count == 0)
            return null;

        foreach (var attribute in duration)
            if (attribute is not (CooldownReductionAttribute or HasteAttribute))
                throw new InvalidOperationException(
                    $"Ability {ability.Id} scales its cooldown duration off '{attribute}', which the cooldown scaling does not model.");

        foreach (var attribute in timeRate)
            if (attribute is not (CooldownRecoveryAttribute or HasteAttribute))
                throw new InvalidOperationException(
                    $"Ability {ability.Id} scales its cooldown time rate off '{attribute}', which the cooldown scaling does not model.");

        return new CooldownScaling
        {
            DurationCooldownReduction = duration.Contains(CooldownReductionAttribute),
            DurationHaste = duration.Contains(HasteAttribute),
            TimeRateCooldownRecovery = timeRate.Contains(CooldownRecoveryAttribute),
            TimeRateHaste = timeRate.Contains(HasteAttribute),
        };
    }

    private static EntityRef? Reference(Fellowship.SDK.Models.EntityReference? reference) =>
        reference is null ? null : new EntityRef(EntityTypes.Slug(reference.Type), reference.Id, reference.Name);

    private static int? Whole(double? value) => value is { } number ? (int)number : null;
}
