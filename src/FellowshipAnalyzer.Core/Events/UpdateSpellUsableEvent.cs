namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public class UpdateSpellUsableEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    /// <summary>
    /// The kind of update to spell usability this represents.
    /// </summary>
    public virtual UpdateSpellUsableType UpdateType { get; set; }
    /// <summary>
    /// If the spell is on cooldown <i>after</i> the event
    /// </summary>
    public virtual bool IsOnCooldown { get; set; }
    /// <summary>
    /// If the spell is available <i>after</i> the event
    /// </summary>
    public virtual bool IsAvailable { get; set; }
    /// <summary>
    /// How many charges are available <i>after</i> the event
    /// </summary>
    public virtual int ChargesAvailable { get; set; }
    /// <summary>
    /// The maximum number of charges for the spell
    /// </summary>
    public virtual int MaxCharges { get; set; }
    /// <summary>
    /// The timestamp this cooldown began - always the timestamp of the most recent BeginCooldown
    /// for this ability, and if this is a BeginCooldown it will be the same as this event's timestamp. <br />
    /// Note this is the overall beginning, <i>not</i> the beginning of the most recent charge's cooldown.
    /// </summary>
    public virtual int OverallStartTimestamp { get; set; }
    /// <summary>
    /// The timestamp the cooldown of this charge began - always the timestamp of the most recent
    /// BeginCooldown or RestoreCharge for this ability. If this is a BeginCooldown it will be the
    /// same as the event's timestamp.
    /// </summary>
    public virtual int ChargeStartTimestamp { get; set; }
    /// <summary>
    /// The timestamp the next charge is expected to be restored, based on current conditions. <br />
    /// For a spell without charges, this is the expected time of the cooldown's end. 
    /// (for an EndCooldown updateType, it will be the actual time of the cooldown's end - this event's timestamp) <br />
    /// Expectation is based on the current haste / modRate / buffs - for dynamic cooldowns
    /// the actual recharge time may be earlier* or* later.
    /// </summary>
    public virtual int ExpectedRechargeTimestamp { get; set; }
    /// <summary>
    /// The expected time it will take for a full charge to recover, based on current conditions. <br />
    /// This is <i>not</i> the time from the current timestamp expected, but the time from charge start
    /// to finish. Expectation is based on the current haste / modRate / buffs - for dynamic cooldowns
    /// the actual recharge time may be earlier or later.
    /// </summary>
    public virtual int ExpectedRechargeDuration { get; set; }

    /** The time after an ability's cooldown ends that the player has to wait in GCD before
     *  being able to cast the spell. Useful for making the spell utilization numbers more fair
     *  when a GCD spell resets another spell's cooldown.
     *  Only filled in for EndCooldown updateType. */
    public virtual int? TimeWaitingOnGCD { get; set; }
    /// <summary>
    /// In practice will always be the selected player - included to make filtering easier
    /// </summary>
    public virtual int SourceId { get; set; }
    /// <summary>
    /// In practice will always be true - included to make filtering easier
    /// </summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>
    /// In practice will always be the selected player - included to make filtering easier
    /// </summary>
    public virtual int TargetId { get; set; }
    /// <summary>
    /// In practice will always be true - included to make filtering easier
    /// </summary>
    public virtual bool? TargetIsFriendly { get; set; }
    public override bool? Fabricated => true;
}
