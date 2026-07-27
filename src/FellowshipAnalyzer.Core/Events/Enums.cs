namespace FellowshipAnalyzer.Core.Events;

/// <summary>How a damage, heal, or ability event resolved against its target.</summary>
public enum HitTypeEnum
{
    /// <summary>The attack failed to land.</summary>
    Miss = 0,
    /// <summary>A regular, non-critical hit.</summary>
    Normal = 1,
    /// <summary>A critical hit.</summary>
    Crit = 2,
    /// <summary>The amount was absorbed by a shield rather than applied directly.</summary>
    Absorb = 3,
    /// <summary>A normal hit that was partially blocked.</summary>
    BlockedNormal = 4,
    /// <summary>A critical hit that was partially blocked.</summary>
    BlockedCrit = 5,
    /// <summary>The target dodged the attack.</summary>
    Dodge = 7,
    /// <summary>The target parried the attack.</summary>
    Parry = 8,
    /// <summary>The target was immune to the effect.</summary>
    Immune = 10
}

/// <summary>The kind of actor an event's source or target is.</summary>
public enum UnitTypeEnum
{
    /// <summary>A player character.</summary>
    Player,
    /// <summary>A non-player enemy.</summary>
    NPC,
    /// <summary>A pet or summoned unit belonging to a player.</summary>
    Pet
}

/// <summary>The kind of change a <see cref="UpdateSpellUsableEvent"/> represents in a spell's cooldown lifecycle.</summary>
public enum UpdateSpellUsableType
{
    /// <summary>The spell's cooldown started.</summary>
    BeginCooldown,
    /// <summary>The spell's cooldown finished.</summary>
    EndCooldown,
    /// <summary>A charge of the spell was consumed.</summary>
    UseCharge,
    /// <summary>A charge of the spell was restored.</summary>
    RestoreCharge,
    /// <summary>The rate at which the spell's cooldown recovers changed.</summary>
    ChangeCooldownRate
}
