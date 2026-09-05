using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Fabricated by <see cref="StatTracker"/> when a <see cref="CooldownModifier"/> is added to or removed
/// from one of the tracked cooldown stat pools. Includes the modifier itself so subscribers can compute
/// the per-ability rate change; <see cref="SpellUsable"/> uses it to rescale in-flight cooldowns when
/// the Cooldown Acceleration pool changes.
/// </summary>
[Fabricated]
public class ChangeCooldownModifierEvent : Event
{
    /// <summary>Always the selected player's actor id: only the player's own cooldown pools are tracked.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>Always the selected player's actor id: only the player's own cooldown pools are tracked.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Always true, since the target is always the selected player.</summary>
    public virtual bool? TargetIsFriendly => true;
    /// <summary>Which of <see cref="StatTracker"/>'s tracked cooldown stat pools the modifier changed.</summary>
    public virtual CooldownPool Pool { get; set; }

    /// <summary>
    /// The modifier that changed. Excluded from serialization because a <see cref="CooldownScope"/> can
    /// be an arbitrary predicate.
    /// </summary>
    [JsonIgnore]
    public virtual CooldownModifier Modifier { get; set; } = null!;
    /// <summary>True when <see cref="Modifier"/> was added to the pool; false when it was removed.</summary>
    public virtual bool Added { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
