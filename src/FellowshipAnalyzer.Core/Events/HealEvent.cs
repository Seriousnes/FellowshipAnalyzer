using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>A heal on a target, naming the healer, the target, and how much of the healing was effective versus overhealed.</summary>
public class HealEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, IAmountEvent
{
    /// <summary>
    /// Unique Identifier for the source. Nobody else will have this ID
    /// </summary>
    public virtual int SourceId { get; set; }
    /// <summary>Disambiguates <see cref="SourceId"/> when multiple copies of the healer exist.</summary>
    public virtual int? SourceInstance { get; set; }
    /// <summary>
    /// If the person who is doing the healing friendly
    /// </summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>
    /// Unique Identifier for the target. Nobody else will have this ID
    /// </summary>
    public virtual int TargetId { get; set; }
    /// <summary>Disambiguates <see cref="TargetId"/> when multiple copies of the healed target exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>
    /// Is the target you're healing a friendly
    /// </summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The ability that is healing the target
    /// </summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID identifying <see cref="Ability"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>
    /// This describes if the spell Hit/Missed/Crit/etc. Look at <see cref="Common.HitType"/> all types of hits
    /// </summary>
    public virtual HitType HitType { get; set; }
    /// <summary>
    /// The effective healing the event did
    /// </summary>
    public virtual long Amount { get; set; }
    /// <summary>
    /// The overheal the event did
    /// </summary>
    public virtual long? Overheal { get; set; }
    /// <summary>Indicates whether the heal was a critical heal.</summary>
    public bool IsCritical => HitType is HitType.Crit or HitType.GrievousCrit;
}

/// <summary>A heal mirrored onto a beacon-bound target, redirecting part of the healing in <see cref="OriginalHeal"/>.</summary>
public class BeaconHealEvent : HealEvent
{
    /// <summary>The heal event whose healing was mirrored onto the beacon-bound target.</summary>
    public virtual HealEvent OriginalHeal { get; set; }
}

/// <summary>Reports that a beacon heal could not be mirrored onto its bound target.</summary>
public class BeaconTransferFailedEvent : HealEvent { }

/// <summary>A heal delivered through a feed effect, where <see cref="Feed"/> is the effect-specific value, separate from <see cref="HealEvent.Amount"/>.</summary>
public class FeedHealEvent : HealEvent
{
    /// <summary>The effect-specific value of this feed heal.</summary>
    public virtual int Feed { get; set; }
}