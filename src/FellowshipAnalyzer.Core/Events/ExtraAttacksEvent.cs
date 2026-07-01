using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

public class ExtraAttacksEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    public virtual Ability Ability { get; set; }
    public virtual FSLID AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int? SourceMarker { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual int? TargetMarker { get; set; }
    public virtual int ExtraAttacks { get; set; }
}
