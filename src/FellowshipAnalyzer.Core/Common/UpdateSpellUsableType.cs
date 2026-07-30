using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Common;

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
