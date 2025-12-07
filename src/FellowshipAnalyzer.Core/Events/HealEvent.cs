namespace FellowshipAnalyzer.Core.Events;

public class HealEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, IAmountEvent
{
    /// <summary>
    /// Unique Identifier for the source. Nobody else will have this ID
    /// </summary>
    public virtual int SourceId { get; set; }
    public virtual int? SourceInstance { get; set; }
    /// <summary>
    /// If the person who is doing the healing friendly
    /// </summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>
    /// Unique Identifier for the target. Nobody else will have this ID
    /// </summary>
    public virtual int TargetId { get; set; }
    public virtual int? TargetInstance { get; set; }
    /// <summary>
    /// Is the target you're healing a friendly
    /// </summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The ability that is healing the target
    /// </summary>
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    /// <summary>
    /// This describes if the spell Hit/Missed/Crit/etc. Look at <see cref="HitTypeEnum"/> all types of hits
    /// </summary>
    public virtual HitTypeEnum HitType { get; set; }
    /// <summary>
    /// The effective healing the event did
    /// </summary>
    public virtual long Amount { get; set; }
    /// <summary>
    /// The overheal the event did
    /// </summary>
    public virtual long? Overheal { get; set; }
    
}

public class BeaconHealEvent : HealEvent
{
    public virtual HealEvent OriginalHeal { get; set; }
}

public class BeaconTransferFailedEvent : HealEvent { }

public class FeedHealEvent : HealEvent
{
    public virtual int Feed { get; set; }
}