using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public class AuraBrokenEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    public virtual Ability Ability { get; set; } = null!;
    public virtual FSLID AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
}
