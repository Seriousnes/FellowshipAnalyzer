using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public class BeginChannelEvent : Event, IAbilityEvent, IHasSourceEvent
{
    public virtual Ability Ability { get; set; }
    public virtual FSLID AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool IsCancelled { get; set; }
    public virtual int? TargetId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual object? Meta { get; set; }
    public virtual GlobalCooldownEvent? GlobalCooldown { get; set; }
}
