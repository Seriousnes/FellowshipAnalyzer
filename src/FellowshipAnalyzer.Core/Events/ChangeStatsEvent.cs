namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Fabricated by <see cref="Analysis.StatTracker"/> whenever the selected
/// player's stat ratings change, with the full stats before and after the change.
/// </summary>
[Fabricated]
public class ChangeStatsEvent : Event
{
    /// <summary>The selected player's actor id; stat changes are always self-sourced.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>The selected player's actor id, always the same as <see cref="SourceId"/>.</summary>
    public virtual int TargetId { get; set; }
    /// <summary>Always <c>true</c>: stat changes are fabricated only for the friendly, selected player.</summary>
    public virtual bool? TargetIsFriendly => true;
    /// <summary>The player's full stats immediately after the change.</summary>
    public virtual Stats After { get; set; }
    /// <summary>The player's full stats immediately before the change.</summary>
    public virtual Stats Before { get; set; }
    /// <summary>The per-stat difference between <see cref="Before"/> and <see cref="After"/>.</summary>
    public virtual Stats Delta { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}

/// <summary>
/// Fabricated by <see cref="Analysis.Haste"/> whenever the selected player's
/// total effective haste percentage changes.
/// </summary>
public class ChangeHasteEvent : ChangeStatsEvent
{
    /// <summary>
    /// The total effective haste, as a decimal fraction (0.30 = 30%), immediately before this change; null on
    /// the dungeon-start event, where the haste the player brought in has no prior value to move from.
    /// </summary>
    public virtual double? OldHaste { get; set; }
    /// <summary>The total effective haste, as a decimal fraction (0.30 = 30%), immediately after this change.</summary>
    public virtual double? NewHaste { get; set; }
}