using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public class EndChannelEvent : Event, IAbilityEvent, IHasSourceEvent
{
    public virtual Ability Ability { get; set; }
    public virtual FSLID AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int Start { get; set; }
    public virtual int Duration { get; set; }
    public virtual BeginChannelEvent BeginChannel { get; set; }
}