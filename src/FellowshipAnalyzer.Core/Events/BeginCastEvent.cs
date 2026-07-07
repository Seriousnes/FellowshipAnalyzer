using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public class BeginCastEvent : Event, IAbilityEvent, IHasSourceEvent
{
    public virtual Ability Ability { get; set; }
    public virtual FSLID AbilityGameId { get; set; }
    public virtual CastEvent? CastEvent { get; set; }
    public virtual BeginChannelEvent? Channel { get; set; }
    public virtual bool IsCancelled { get; set; }
    public virtual int SourceId { get; set; }
    public virtual ICastTarget? Target { get; set; }
}
