using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

public record CombatantInfoEvent : Event
{
    public int Fight { get; set; }
    public int SourceId { get; set; }
    public List<Item> Gear { get; set; } = [];
    public List<Aura> Auras { get; set; } = [];
    public List<TalentInfo> Talents { get; set; } = [];
    public int WeaponTreeId { get; set; }
    public List<WeaponTrait> WeaponTraits { get; set; } = [];

    // Primary stats
    public int ItemLevel { get; set; }
    public int Health { get; set; }
    public int Mana { get; set; }
    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Intellect { get; set; }
    public int Stamina { get; set; }
    public int Armor { get; set; }

    // Secondary stats
    public int Crit { get; set; }
    public int Haste { get; set; }
    public int Expertise { get; set; }
    public int Spirit { get; set; }

    // Gem powers
    public int Amethyst { get; set; }
    public int Diamond { get; set; }
    public int Topaz { get; set; }
    public int Ruby { get; set; }
    public int Sapphire { get; set; }
    public int Emerald { get; set; }

    [JsonIgnore]
    public decimal ComputedItemLevel => ItemLevel / 10m;
}

public record Aura
{
    public int Source { get; set; }
    public int Ability { get; set; }
    public int Stacks { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public record TalentInfo
{
    public int Id { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public record WeaponTrait
{
    public int Id { get; set; }
    public int Level { get; set; }
    public int Points { get; set; }
}

public record Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quality { get; set; }
    public string Icon { get; set; } = string.Empty;
    public int ItemLevel { get; set; }
    public int Upgrades { get; set; }
    public int MaxUpgrades { get; set; }
    public bool HasGemSocket { get; set; }
    public ItemGem? Gem { get; set; }
    public List<ItemAttribute> Attributes { get; set; } = [];
    public ItemSet? Set { get; set; }

    public static implicit operator int(Item i) => i.Id;
}

public record ItemSet
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public record ItemGem
{
    public int Id { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quality { get; set; }
}

public record ItemAttribute
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}