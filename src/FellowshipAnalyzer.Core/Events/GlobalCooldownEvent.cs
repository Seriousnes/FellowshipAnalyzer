using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Fabricated by <see cref="Analysis.GlobalCooldown"/> for every on-GCD cast or channel start, and attached
/// to the triggering event so Timeline rendering can display GCD bars without re-scanning all events.
/// </summary>
[Fabricated]
public class GlobalCooldownEvent : Event, IAbilityEvent
{
    /// <summary>The ability whose cast or channel triggered this global cooldown.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID identifying <see cref="Ability"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>The length of the global cooldown, in milliseconds.</summary>
    public virtual int Duration { get; set; }
    /// <summary>Unique identifier for the source. Nobody else will have this ID.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Unique identifier for the target. Nobody else will have this ID.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Whether the target is friendly.</summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>The event that started this global cooldown, narrowing <see cref="Event.Trigger"/>.</summary>
    public virtual new ICooldownTriggerEvent? Trigger { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
