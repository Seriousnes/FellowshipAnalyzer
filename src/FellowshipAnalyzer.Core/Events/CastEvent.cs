namespace FellowshipAnalyzer.Core.Events;

public abstract record BaseCastEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, ISpellPowerEvent
{
    public virtual Ability Ability { get; set; }
    public int AbilityGameId { get; set; }
    public virtual int? Absorb { get; set; }
    public virtual EndChannelEvent? Channel { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int? SourceInstance { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int SpellPower { get; set; }
    public virtual ICastTarget Target { get; set; }
    public virtual int TargetId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual Dictionary<int, int>? RawResourceCost { get; set; }
    public virtual Dictionary<int, int>? ResourceCost { get; set; }
    public virtual GlobalCooldownEvent? GlobalCooldown { get; set; }
    public virtual object? Meta { get; set; }
}

public record CastEvent : BaseCastEvent
{
    /// <summary>FellowshipLogs synthetic event — not a real player action.</summary>
    public virtual bool Fake { get; set; }

    /// <summary>FellowshipLogs cast-start marker (beginning of a cast with cast time).</summary>
    public virtual bool Activation { get; set; }
}
public record FreeCastEvent : BaseCastEvent { }
public record LeechEvent : BaseCastEvent { }
public record FilterCooldownInfoEvent : BaseCastEvent
{
}