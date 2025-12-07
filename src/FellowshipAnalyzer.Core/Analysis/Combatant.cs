using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Represents a player entity with gear, stats, talents, and buff history.
/// Populated from a <see cref="CombatantInfoEvent"/> at the start of analysis.
/// </summary>
public sealed class Combatant : Entity
{
    private readonly Dictionary<GearSlot, Item> _gear;
    private readonly Dictionary<int, Item> _itemById;

    public Combatant(CombatantInfoEvent info)
    {
        Info = info;

        _gear = info.Gear
            .Select((item, index) => (item, slot: (GearSlot)index))
            .ToDictionary(x => x.slot, x => x.item);

        _itemById = _gear.Values.ToDictionary(i => i.Id);
    }

    public int Id => Info.SourceId;
    public CombatantInfoEvent Info { get; }

    // Stats
    public decimal ItemLevel => Info.ComputedItemLevel;
    public int Health => Info.Health;
    public int Mana => Info.Mana;
    public int Strength => Info.Strength;
    public int Agility => Info.Agility;
    public int Intellect => Info.Intellect;
    public int Stamina => Info.Stamina;
    public int Armor => Info.Armor;
    public int Crit => Info.Crit;
    public int Haste => Info.Haste;
    public int Expertise => Info.Expertise;
    public int Spirit => Info.Spirit;

    // Gem powers
    public int Amethyst => Info.Amethyst;
    public int Diamond => Info.Diamond;
    public int Topaz => Info.Topaz;
    public int Ruby => Info.Ruby;
    public int Sapphire => Info.Sapphire;
    public int Emerald => Info.Emerald;

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
    public bool HasTalent(int talentId) => Info.Talents.Any(t => t.Id == talentId);

    // Auras (prepull buffs)
    public IReadOnlyList<Aura> Auras => Info.Auras;

    // Weapon traits
    public IReadOnlyList<WeaponTrait> WeaponTraits => Info.WeaponTraits;

    private Item? GetSlot(GearSlot slot) => _gear.GetValueOrDefault(slot);
}
