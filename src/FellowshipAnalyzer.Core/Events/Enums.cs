namespace FellowshipAnalyzer.Core.Events;

/// <summary>How a damage, heal, or ability event resolved against its target.</summary>
public enum HitType
{
    /// <summary>The attack missed, dealing no damage.</summary>
    Miss = 0,
    /// <summary>A regular, non-critical hit.</summary>
    Normal = 1,
    /// <summary>A critical hit.</summary>
    Crit = 2,
    /// <summary>
    /// A hit the target blocked. Carries a non-zero <see cref="DamageEvent.Blocked"/>, and often
    /// reduces the hit to nothing; only heroes with a block chance produce it.
    /// </summary>
    Block = 4,
    /// <summary>The attack was dodged, dealing no damage.</summary>
    Dodge = 7,
    /// <summary>The attack was parried, dealing no damage.</summary>
    Parry = 8,
    /// <summary>Guarantee crit dealing additional damage based on crit chance.</summary>
    GrievousCrit = 22
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
