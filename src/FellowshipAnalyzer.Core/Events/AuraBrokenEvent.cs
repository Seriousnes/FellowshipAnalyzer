using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// A crowd-control style aura on the target ended early, for example a stun or root broken by
/// the target taking damage, rather than expiring naturally or being dispelled.
/// </summary>
public class AuraBrokenEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    /// <summary>The aura that was broken.</summary>
    public virtual Ability Ability { get; set; } = null!;

    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }

    /// <summary>The id of the actor whose action caused the aura to break.</summary>
    public virtual int SourceId { get; set; }

    /// <summary>The instance id of the actor the aura was applied to, when multiple copies of that actor exist.</summary>
    public virtual int? TargetInstance { get; set; }

    /// <summary>The id of the actor the aura was applied to.</summary>
    public virtual int TargetId { get; set; }
}
