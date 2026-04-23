namespace FellowshipAnalyzer.Core.Events;

public class DispelEvent : Event, IAbilityEvent, IExtraAbilityEvent, IHasSourceEvent, IHasTargetWithInstanceEvent
{
    /// <summary>
    /// The ability used to dispel <see cref="ExtraAbility"/>
    /// </summary>
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    /// <summary>
    /// The ability dispelled by <see cref="Ability"/>
    /// </summary>
    public virtual Ability ExtraAbility { get; set; }
    public virtual int ExtraAbilityGameId { get; set; }
    public virtual bool IsBuff { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
}