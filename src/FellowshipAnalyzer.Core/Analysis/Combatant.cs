using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// One combat unit the dungeon names, identified by its actor id and carrying the aura history
/// <see cref="Entity"/> tracks. A group member other than the analyzed player is a plain
/// <see cref="Combatant"/>; an enemy is an <see cref="Enemy"/>, which adds its spawn instance; the
/// analyzed player is a <see cref="FullCombatant"/>, the only one a
/// <see cref="CombatantInfoEvent"/> populates and so the only one with gear, talents, and stats.
/// </summary>
public class Combatant(int id) : Entity
{
    /// <summary>The unit's actor id, as it appears throughout the event stream.</summary>
    public int Id { get; init; } = id;
}

/// <summary>
/// The analyzed player, with gear, talents, and buff history. Populated from a
/// <see cref="CombatantInfoEvent"/> at the start of analysis. The player's stat ratings and derived
/// cooldown values are exposed as a frozen <see cref="CombatantStats"/> snapshot on <see cref="Stats"/>,
/// built once from the combatantinfo.
/// </summary>
public sealed class FullCombatant : Combatant
{
    private readonly Dictionary<GearSlot, Item> _gear;
    private readonly Dictionary<int, Item> _itemById;

    private static readonly GemRank[] BlessingOfTheCommander =
    [
        new(RequiredGemPower: 450, Magnitude: 0.04),
        new(RequiredGemPower: 1500, Magnitude: 0.12),
    ];

    private const int LegendaryQuality = 6;
    private const double StrandOfEternityAcceleration = 0.10;

    /// <summary>Builds the combatant's gear index and computes its derived <see cref="Stats"/> snapshot from the given combatantinfo.</summary>
    public FullCombatant(CombatantInfoEvent info) : base(info.SourceId)
    {
        Info = info;

        _gear = info.Gear
            .Select((item, index) => (item, slot: (GearSlot)index))
            .ToDictionary(x => x.slot, x => x.item);

        _itemById = _gear.Values.ToDictionary(i => i.Id);

        HasLegendary = info.Gear.Any(item => item.Quality >= LegendaryQuality);
        Stats = BuildStats(info);
    }

    /// <summary>The raw <see cref="CombatantInfoEvent"/> this combatant was built from.</summary>
    public CombatantInfoEvent Info { get; }

    /// <summary>Frozen snapshot of the player's stat ratings and derived cooldown values, built once from the combatantinfo.</summary>
    public CombatantStats Stats { get; init; }

    /// <summary>The combatant's average equipped item level.</summary>
    public decimal ItemLevel => Info.ComputedItemLevel;

    /// <summary>Total gem power of socketed Amethyst gems.</summary>
    public int Amethyst => Info.Amethyst;

    /// <summary>Total gem power of socketed Diamond gems.</summary>
    public int Diamond => Info.Diamond;

    /// <summary>Total gem power of socketed Topaz gems.</summary>
    public int Topaz => Info.Topaz;

    /// <summary>Total gem power of socketed Ruby gems.</summary>
    public int Ruby => Info.Ruby;

    /// <summary>Total gem power of socketed Sapphire gems.</summary>
    public int Sapphire => Info.Sapphire;

    /// <summary>Total gem power of socketed Emerald gems, the input to <see cref="CombatantStats.AbilityCooldownReduction"/>.</summary>
    public int Emerald => Info.Emerald;

    /// <summary>True when any equipped item is legendary quality (the top rarity, of which only one may be equipped).</summary>
    public bool HasLegendary { get; }

    /// <summary>The item equipped in the head slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Head => GetSlot(GearSlot.Head);

    /// <summary>The item equipped in the necklace slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Necklace => GetSlot(GearSlot.Necklace);

    /// <summary>The item equipped in the shoulders slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Shoulders => GetSlot(GearSlot.Shoulders);

    /// <summary>The item equipped in the back slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Back => GetSlot(GearSlot.Back);

    /// <summary>The item equipped in the chest slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Chest => GetSlot(GearSlot.Chest);

    /// <summary>The item equipped in the wrists slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Wrists => GetSlot(GearSlot.Wrists);

    /// <summary>The item equipped in the hands slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Hands => GetSlot(GearSlot.Hands);

    /// <summary>The item equipped in the legs slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Legs => GetSlot(GearSlot.Legs);

    /// <summary>The item equipped in the feet slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Feet => GetSlot(GearSlot.Feet);

    /// <summary>The item equipped in the first ring slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Ring1 => GetSlot(GearSlot.Ring1);

    /// <summary>The item equipped in the second ring slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Ring2 => GetSlot(GearSlot.Ring2);

    /// <summary>The item equipped in the first relic slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Relic1 => GetSlot(GearSlot.Relic1);

    /// <summary>The item equipped in the second relic slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Relic2 => GetSlot(GearSlot.Relic2);

    /// <summary>The item equipped in the weapon slot, or <c>null</c> if that slot is empty.</summary>
    public Item? Weapon => GetSlot(GearSlot.Weapon);

    /// <summary>All equipped items, in the order the combatantinfo reported them.</summary>
    public IReadOnlyList<Item> Gear => Info.Gear;

    /// <summary>Looks up an equipped item by its item id, or <c>null</c> if no equipped item has that id.</summary>
    public Item? GetItem(int itemId) => _itemById.GetValueOrDefault(itemId);

    /// <summary>True when an item with the given item id is equipped.</summary>
    public bool HasItem(int itemId) => _itemById.ContainsKey(itemId);

    /// <summary>The talents the player had selected, as recorded in the combatantinfo.</summary>
    public IReadOnlyList<TalentInfo> Talents => Info.Talents;

    /// <summary>
    /// True when the player has the talent with the given native id. Combat logs store talent ids in
    /// the FSL Talent namespace (<c>2,000,000 + native</c>), so the stored id is decoded before comparison.
    /// </summary>
    public bool HasTalent(int talentId) => Info.Talents.Any(t => new FSLID(t.Id).NativeId == talentId);

    /// <summary>Buffs and debuffs active on the player at combatantinfo time.</summary>
    public IReadOnlyList<Aura> Auras => Info.Auras;

    /// <summary>The player's unlocked weapon tree traits and their point allocations.</summary>
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
/// Frozen snapshot of a <see cref="FullCombatant"/>'s stat ratings and derived cooldown values, built once from
/// the <see cref="CombatantInfoEvent"/> when the combatant is constructed. The rating fields are copied
/// straight from the combatantinfo; the cooldown fields are computed from gem-power rank unlocks and equipped
/// gear.
/// </summary>
public sealed record CombatantStats
{
    /// <summary>The player's health rating, copied from the combatantinfo.</summary>
    public int Health { get; init; }

    /// <summary>The player's mana rating, copied from the combatantinfo.</summary>
    public int Mana { get; init; }

    /// <summary>The player's strength rating, copied from the combatantinfo.</summary>
    public int Strength { get; init; }

    /// <summary>The player's agility rating, copied from the combatantinfo.</summary>
    public int Agility { get; init; }

    /// <summary>The player's intellect rating, copied from the combatantinfo.</summary>
    public int Intellect { get; init; }

    /// <summary>The player's stamina rating, copied from the combatantinfo.</summary>
    public int Stamina { get; init; }

    /// <summary>The player's armor rating, copied from the combatantinfo.</summary>
    public int Armor { get; init; }

    /// <summary>The player's critical strike rating, copied from the combatantinfo.</summary>
    public int Crit { get; init; }

    /// <summary>The player's haste rating, copied from the combatantinfo.</summary>
    public int Haste { get; init; }

    /// <summary>The player's expertise rating, copied from the combatantinfo.</summary>
    public int Expertise { get; init; }

    /// <summary>The player's spirit rating, copied from the combatantinfo.</summary>
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
