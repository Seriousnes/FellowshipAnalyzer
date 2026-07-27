using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Fabricated when an actor's auto-attack begins its swing cooldown, so the auto-attack
/// participates in the same cooldown-tracking pipeline as ability cooldowns.
/// </summary>
[Fabricated]
public class AutoAttackCooldownEvent : Event, IAbilityEvent
{
    /// <summary>The auto-attack ability this cooldown applies to.</summary>
    public virtual Ability Ability { get; set; } = null!;

    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }

    /// <summary>The swing timer length, in seconds, before the auto-attack is ready again.</summary>
    public virtual double Duration { get; set; }

    /// <summary>The haste value in effect when the cooldown began, used to compute <see cref="Duration"/>.</summary>
    public virtual double Haste { get; set; }

    /// <summary>The attack speed value in effect when the cooldown began, used to compute <see cref="Duration"/>.</summary>
    public virtual double AttackSpeed { get; set; }

    /// <summary>The id of the actor whose auto-attack this is.</summary>
    public virtual int SourceId { get; set; }

    /// <summary>The id of the actor the auto-attack was aimed at.</summary>
    public virtual int TargetId { get; set; }

    /// <summary>The event that started this auto-attack's cooldown, narrowing <see cref="Event.Trigger"/>.</summary>
    public new ICooldownTriggerEvent Trigger { get; set; }

    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
