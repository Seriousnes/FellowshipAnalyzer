namespace FellowshipAnalyzer.Core.Events;

public record BeginChannelEvent : Event, IAbilityEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool IsCancelled { get; set; }
    public virtual int? TargetId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual object? Meta { get; set; }
    public virtual GlobalCooldownEvent? GlobalCooldown { get; set; }
}
