using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks the start of a channeled ability cast.</summary>
public class BeginChannelEvent : Event, IAbilityEvent, IHasSourceEvent
{
    /// <summary>The ability being channeled.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID identifying <see cref="Ability"/> in the FellowshipLogs game data.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>
    /// Unique Identifier for the source. Nobody else will have this ID
    /// </summary>
    public virtual int SourceId { get; set; }
    /// <summary>Whether the channel was cancelled before it completed.</summary>
    public virtual bool IsCancelled { get; set; }
    /// <summary>Unique identifier for the target, if the channel is aimed at one.</summary>
    public virtual int? TargetId { get; set; }
    /// <summary>Disambiguates <see cref="TargetId"/> when multiple copies of the same target exist.</summary>
    public virtual int? TargetInstance { get; set; }
    /// <summary>Additional ability-specific payload supplied by FellowshipLogs for this channel.</summary>
    public virtual object? Meta { get; set; }
    /// <summary>The global cooldown event triggered by starting this channel, if one was linked.</summary>
    public virtual GlobalCooldownEvent? GlobalCooldown { get; set; }
    /// <summary>The <see cref="EndChannelEvent"/> that closes out this channel, once linked.</summary>
    public virtual EndChannelEvent? EndChannel { get; set; }
}
