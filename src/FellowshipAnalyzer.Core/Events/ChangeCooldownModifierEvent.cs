using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Fabricated by <see cref="StatTracker"/> when a <see cref="CooldownModifier"/> is added to or removed
/// from one of the tracked cooldown stat pools. Carries the modifier itself so subscribers can compute
/// the per-ability rate change; <see cref="SpellUsable"/> uses it to rescale in-flight cooldowns when
/// the Cooldown Acceleration pool changes.
/// </summary>
[Fabricated]
public class ChangeCooldownModifierEvent : Event
{
    public virtual int SourceId { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly => true;
    public virtual CooldownPool Pool { get; set; }

    /// <summary>
    /// The modifier that changed. Excluded from serialization because a <see cref="CooldownScope"/> can
    /// carry an arbitrary predicate.
    /// </summary>
    [JsonIgnore]
    public virtual CooldownModifier Modifier { get; set; } = null!;
    public virtual bool Added { get; set; }
    public override bool? Fabricated => true;
}
