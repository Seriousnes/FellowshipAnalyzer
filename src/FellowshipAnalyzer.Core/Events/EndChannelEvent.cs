using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Fired when a channeled ability's channel ends, whether it completed naturally or was cancelled.</summary>
public class EndChannelEvent : Event, IAbilityEvent, IHasSourceEvent
{
    /// <summary>The ability that was channeled.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID of the channeled ability.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>The id of the actor whose channel this is.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>The timestamp the channel began.</summary>
    public virtual int Start { get; set; }
    /// <summary>How long the channel ran for, in milliseconds.</summary>
    public virtual int Duration { get; set; }
    /// <summary>The <see cref="BeginChannelEvent"/> that started this channel.</summary>
    public virtual BeginChannelEvent BeginChannel { get; set; }
}