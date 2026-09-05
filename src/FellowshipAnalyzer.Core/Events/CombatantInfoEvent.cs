using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>The FellowshipLogs <c>combatantinfo</c> event: a player's gear, talents, stat ratings, and active auras, used to build the analysis <see cref="Analysis.Combatant"/>.</summary>
public class CombatantInfoEvent : Event
{
    /// <summary>The player's actor id, in the same id space as the source and target ids used throughout the event stream.</summary>
    public int SourceId { get; set; }
    /// <summary>The items equipped by the player at combatantinfo time, ordered by gear slot.</summary>
    public List<Item> Gear { get; set; } = [];
    /// <summary>Buffs and debuffs active on the player at combatantinfo time.</summary>
    public List<Aura> Auras { get; set; } = [];
    /// <summary>The talents the player had selected at combatantinfo time.</summary>
    public List<TalentInfo> Talents { get; set; } = [];
    /// <summary>The id of the weapon tree the player's weapon traits were allocated from.</summary>
    public int WeaponTreeId { get; set; }
    /// <summary>The player's unlocked weapon tree traits and their point allocations.</summary>
    public List<WeaponTrait> WeaponTraits { get; set; } = [];

    /// <summary>The player's raw item level rating, ten times the displayed value; see <see cref="ComputedItemLevel"/> for the displayed form.</summary>
    public int ItemLevel { get; set; }
    /// <summary>The player's health rating.</summary>
    public int Health { get; set; }
    /// <summary>The player's mana rating.</summary>
    public int Mana { get; set; }
    /// <summary>The player's strength rating.</summary>
    public int Strength { get; set; }
    /// <summary>The player's agility rating.</summary>
    public int Agility { get; set; }
    /// <summary>The player's intellect rating.</summary>
    public int Intellect { get; set; }
    /// <summary>The player's stamina rating.</summary>
    public int Stamina { get; set; }
    /// <summary>The player's armor rating.</summary>
    public int Armor { get; set; }

    /// <summary>The player's critical strike rating.</summary>
    public int Crit { get; set; }
    /// <summary>The player's haste rating.</summary>
    public int Haste { get; set; }
    /// <summary>The player's expertise rating.</summary>
    public int Expertise { get; set; }
    /// <summary>The player's spirit rating.</summary>
    public int Spirit { get; set; }

    /// <summary>Total gem power of socketed Amethyst gems.</summary>
    public int Amethyst { get; set; }
    /// <summary>Total gem power of socketed Diamond gems.</summary>
    public int Diamond { get; set; }
    /// <summary>Total gem power of socketed Topaz gems.</summary>
    public int Topaz { get; set; }
    /// <summary>Total gem power of socketed Ruby gems.</summary>
    public int Ruby { get; set; }
    /// <summary>Total gem power of socketed Sapphire gems.</summary>
    public int Sapphire { get; set; }
    /// <summary>Total gem power of socketed Emerald gems, the input to Ability Cooldown Reduction.</summary>
    public int Emerald { get; set; }

    /// <summary>The player's average equipped item level, as displayed in game: <see cref="ItemLevel"/> divided by ten.</summary>
    [JsonIgnore]
    public decimal ComputedItemLevel => ItemLevel / 10m;
}

/// <summary>A buff or debuff active on an actor at the time of a <see cref="CombatantInfoEvent"/>.</summary>
public class Aura
{
    /// <summary>The actor id of whoever applied this aura.</summary>
    public int Source { get; set; }
    /// <summary>The id of the ability that applies this aura.</summary>
    public int Ability { get; set; }
    /// <summary>The number of stacks currently applied.</summary>
    public int Stacks { get; set; }
    /// <summary>The icon asset name shown for this aura in FellowshipLogs.</summary>
    public string Icon { get; set; } = string.Empty;
    /// <summary>The display name of this aura.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>A talent the player had selected at <see cref="CombatantInfoEvent"/> time.</summary>
public class TalentInfo
{
    /// <summary>The talent's id, in the FSL Talent id namespace.</summary>
    public int Id { get; set; }
    /// <summary>The icon asset name shown for this talent in FellowshipLogs.</summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>An unlocked weapon tree trait and its point allocation, as reported in a <see cref="CombatantInfoEvent"/>.</summary>
public class WeaponTrait
{
    /// <summary>The trait's id within its weapon tree.</summary>
    public int Id { get; set; }
    /// <summary>The unlocked level of this trait.</summary>
    public int Level { get; set; }
    /// <summary>The number of points the player has allocated into this trait.</summary>
    public int Points { get; set; }
}

/// <summary>An item equipped by the player at <see cref="CombatantInfoEvent"/> time.</summary>
public class Item
{
    /// <summary>The item's id.</summary>
    public int Id { get; set; }
    /// <summary>The item's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The item's rarity tier; the top tier is legendary quality.</summary>
    public int Quality { get; set; }
    /// <summary>The icon asset name shown for this item in FellowshipLogs.</summary>
    public string Icon { get; set; } = string.Empty;
    /// <summary>The item's own item level rating.</summary>
    public int ItemLevel { get; set; }
    /// <summary>The number of upgrade levels currently applied to this item.</summary>
    public int Upgrades { get; set; }
    /// <summary>The maximum number of upgrade levels this item can receive.</summary>
    public int MaxUpgrades { get; set; }
    /// <summary>True when this item has a socket a gem can be placed in.</summary>
    public bool HasGemSocket { get; set; }
    /// <summary>The gem socketed into this item, or <c>null</c> if the socket is empty or the item has none.</summary>
    public ItemGem? Gem { get; set; }
    /// <summary>The bonus attributes rolled onto this item.</summary>
    public List<ItemAttribute> Attributes { get; set; } = [];
    /// <summary>The traits rolled onto this item.</summary>
    public List<ItemTrait> Traits { get; set; } = [];
    /// <summary>The blessings slotted into this item.</summary>
    public List<ItemBlessing> Blessings { get; set; } = [];
    /// <summary>The item set this item belongs to, or <c>null</c> if it is not part of a set.</summary>
    public ItemSet? Set { get; set; }

    /// <summary>Converts an item to its <see cref="Id"/> for id-based lookups and comparisons.</summary>
    public static implicit operator int(Item i) => i.Id;
}

/// <summary>The gear set an <see cref="Item"/> belongs to.</summary>
public class ItemSet
{
    /// <summary>The item set's id.</summary>
    public int Id { get; set; }
    /// <summary>The item set's display name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>A gem socketed into an <see cref="Item"/>.</summary>
public class ItemGem
{
    /// <summary>The gem's id.</summary>
    public int Id { get; set; }
    /// <summary>The icon asset name shown for this gem in FellowshipLogs.</summary>
    public string Icon { get; set; } = string.Empty;
    /// <summary>The gem's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The gem's rarity tier.</summary>
    public int Quality { get; set; }
}

/// <summary>A trait rolled onto an equipped <see cref="Item"/>.</summary>
public class ItemTrait
{
    /// <summary>The trait's id, in the FSL Weapon and Trait id namespace.</summary>
    public int Id { get; set; }
    /// <summary>The trait's unlocked rank.</summary>
    public int Rank { get; set; }
    /// <summary>The trait's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The icon asset name shown for this trait in FellowshipLogs.</summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// A blessing slotted into an equipped <see cref="Item"/>. The same blessing has a different
/// <see cref="Id"/> in each gear slot it can be slotted into, so <see cref="Name"/> is what identifies
/// which blessing it is.
/// </summary>
public class ItemBlessing
{
    /// <summary>The blessing's id, in the FSL Blessing id namespace.</summary>
    public int Id { get; set; }
    /// <summary>The blessing's allocated level, which selects its magnitude.</summary>
    public int Level { get; set; }
    /// <summary>The blessing's display name, for example <c>"The Wayfarer"</c>.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The icon asset name shown for this blessing in FellowshipLogs.</summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>A bonus attribute rolled onto an equipped <see cref="Item"/>.</summary>
public class ItemAttribute
{
    /// <summary>The attribute's id, identifying which stat it modifies.</summary>
    public int Id { get; set; }
    /// <summary>The attribute's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The magnitude of this attribute's bonus.</summary>
    public int Value { get; set; }
}