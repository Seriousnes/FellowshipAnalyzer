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
    Physical = 0,
    Magic = 1,
    Stagger = 1024
}
