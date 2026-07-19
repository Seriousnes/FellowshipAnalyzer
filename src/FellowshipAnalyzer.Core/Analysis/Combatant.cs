using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Represents a player entity with gear, talents, and buff history. Populated from a
/// <see cref="CombatantInfoEvent"/> at the start of analysis. The player's stat ratings and derived
/// cooldown values are exposed as a frozen <see cref="CombatantStats"/> snapshot on <see cref="Stats"/>,
/// built once from the combatantinfo.
/// </summary>
public sealed class Combatant : Entity
{
    private readonly Dictionary<GearSlot, Item> _gear;
    private readonly Dictionary<int, Item> _itemById;

    /// <summary>Emerald "Blessing of the Commander": Ability Cooldown Reduction ranks.</summary>
    private static readonly GemRank[] BlessingOfTheCommander =
    [
        new(RequiredGemPower: 450, Magnitude: 0.04),
        new(RequiredGemPower: 1500, Magnitude: 0.12),
    ];

    /// <summary>Quality tier of a legendary item; the top rarity, of which only one may be equipped.</summary>
    private const int LegendaryQuality = 6;

    /// <summary>The cooldown acceleration a legendary's Strand of Eternity grants, as a fraction (0.10 = +10%).</summary>
    private const double StrandOfEternityAcceleration = 0.10;

    public Combatant(CombatantInfoEvent info)
    {
        Info = info;

        _gear = info.Gear
            .Select((item, index) => (item, slot: (GearSlot)index))
            .ToDictionary(x => x.slot, x => x.item);

        _itemById = _gear.Values.ToDictionary(i => i.Id);

        HasLegendary = info.Gear.Any(item => item.Quality >= LegendaryQuality);
        Stats = BuildStats(info);
    }

    public int Id => Info.SourceId;
    public CombatantInfoEvent Info { get; }

    /// <summary>Frozen snapshot of the player's stat ratings and derived cooldown values, built once from the combatantinfo.</summary>
    public CombatantStats Stats { get; init; }

    public decimal ItemLevel => Info.ComputedItemLevel;

    // Gem powers
    public int Amethyst => Info.Amethyst;
    public int Diamond => Info.Diamond;
    public int Topaz => Info.Topaz;
    public int Ruby => Info.Ruby;
    public int Sapphire => Info.Sapphire;
    public int Emerald => Info.Emerald;

    /// <summary>True when any equipped item is legendary quality (the top rarity, of which only one may be equipped).</summary>
    public bool HasLegendary { get; }

    // Gear slot accessors
    public Item? Head => GetSlot(GearSlot.Head);
    public Item? Necklace => GetSlot(GearSlot.Necklace);
    public Item? Shoulders => GetSlot(GearSlot.Shoulders);
    public Item? Back => GetSlot(GearSlot.Back);
    public Item? Chest => GetSlot(GearSlot.Chest);
    public Item? Wrists => GetSlot(GearSlot.Wrists);
    public Item? Hands => GetSlot(GearSlot.Hands);
    public Item? Legs => GetSlot(GearSlot.Legs);
    public Item? Feet => GetSlot(GearSlot.Feet);
    public Item? Ring1 => GetSlot(GearSlot.Ring1);
    public Item? Ring2 => GetSlot(GearSlot.Ring2);
    public Item? Relic1 => GetSlot(GearSlot.Relic1);
    public Item? Relic2 => GetSlot(GearSlot.Relic2);
    public Item? Weapon => GetSlot(GearSlot.Weapon);

    public IReadOnlyList<Item> Gear => Info.Gear;

    // Gear queries
    public Item? GetItem(int itemId) => _itemById.GetValueOrDefault(itemId);
    public bool HasGear(int itemId) => _itemById.ContainsKey(itemId);

    // Talent queries
    public IReadOnlyList<TalentInfo> Talents => Info.Talents;

    /// <summary>
    /// True when the player has the talent with the given native id. Combat logs store talent ids in
    /// the FSL Talent namespace (<c>2,000,000 + native</c>), so the stored id is decoded before comparison.
    /// </summary>
    public bool HasTalent(int talentId) => Info.Talents.Any(t => new FSLID(t.Id).NativeId == talentId);

    // Auras (prepull buffs)
    public IReadOnlyList<Aura> Auras => Info.Auras;

    // Weapon traits
    public IReadOnlyList<WeaponTrait> WeaponTraits => Info.WeaponTraits;

    private Item? GetSlot(GearSlot slot) => _gear.GetValueOrDefault(slot);

    private CombatantStats BuildStats(CombatantInfoEvent info) => new()
    {
        Health = info.Health,
        Mana = info.Mana,
        Strength = info.Strength,
        Agility = info.Agility,
        Intellect = info.Intellect,
        Stamina = info.Stamina,
        Armor = info.Armor,
        Crit = info.Crit,
        Haste = info.Haste,
        Expertise = info.Expertise,
        Spirit = info.Spirit,
        AbilityCooldownReduction =
            [new CooldownModifier(HighestUnlocked(BlessingOfTheCommander, info.Emerald))],
        CooldownAcceleration = HasLegendary
            ? [new CooldownModifier(StrandOfEternityAcceleration)]
            : [],
    };

    private static double HighestUnlocked(GemRank[] ranks, int gemPower)
    {
        var threshold = 0;
        var magnitude = 0.0;

        foreach (var rank in ranks)
        {
            if (gemPower >= rank.RequiredGemPower && rank.RequiredGemPower >= threshold)
                (threshold, magnitude) = (rank.RequiredGemPower, rank.Magnitude);
        }

        return magnitude;
    }

    private readonly record struct GemRank(int RequiredGemPower, double Magnitude);
}

/// <summary>
/// Frozen snapshot of a <see cref="Combatant"/>'s stat ratings and derived cooldown values, built once from
/// the <see cref="CombatantInfoEvent"/> when the combatant is constructed. The rating fields are copied
/// straight from the combatantinfo; the cooldown fields are computed from gem-power rank unlocks and equipped
/// gear.
/// </summary>
public sealed record CombatantStats
{
    public int Health { get; init; }
    public int Mana { get; init; }
    public int Strength { get; init; }
    public int Agility { get; init; }
    public int Intellect { get; init; }
    public int Stamina { get; init; }
    public int Armor { get; init; }
    public int Crit { get; init; }
    public int Haste { get; init; }
    public int Expertise { get; init; }
    public int Spirit { get; init; }

    /// <summary>
    /// Ability Cooldown Reduction modifiers, each a fraction (0.12 = 12%): <c>effective = base * (1 - acr)</c>.
    /// </summary>
    public CooldownModifierSet AbilityCooldownReduction { get; init; } = [];

    /// <summary>
    /// Cooldown Acceleration modifiers, each a fraction (0.10 = +10%): terms on the shared recovery pool
    /// <see cref="SpellUsable.EffectiveRate"/> divides by.
    /// </summary>
    public CooldownModifierSet CooldownAcceleration { get; init; } = [];
}
