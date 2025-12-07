namespace FellowshipAnalyzer.Core.Events;

public enum HitTypeEnum
{
    Miss = 0,
    Normal = 1,
    Crit = 2,
    Absorb = 3,
    BlockedNormal = 4,
    BlockedCrit = 5,
    Dodge = 7,
    Parry = 8,
    Immune = 10
}

public enum MapIdEnum { }

public enum ResourceActorEnum
{
    Source = 1,
    Target = 2
}

public enum UnitTypeEnum
{
    Player,
    NPC,
    Pet
}

public enum UpdateSpellUsableType
{
    BeginCooldown,
    EndCooldown,
    UseCharge,
    RestoreCharge
}

[Flags]
public enum MagicSchool
{
    None = 0,
    Physical = 0x1,
    Holy = 0x2,
    Fire = 0x4,
    Nature = 0x8,
    Frost = 0x10,
    Shadow = 0x20,
    Arcane = 0x40
}
