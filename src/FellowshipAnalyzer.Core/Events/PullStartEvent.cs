using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// A single encounter window within a dungeon - the unit of per-encounter analysis - and the event that
/// opens it. Derived from a Fellowship Logs dungeon pull, or fabricated to span a whole dungeon that
/// exposes no pulls. Fabricated by <see cref="Analysis.Normalizers.PullBookendNormalizer"/>; modules hook
/// it via <c>[On&lt;PullStartEvent&gt;]</c>.
/// </summary>
[Fabricated]
public sealed class PullStartEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;

    /// <summary>The zero-based dispatch ordinal of this pull within the dungeon.</summary>
    public int Index { get; init; }

    /// <summary>
    /// The Fellowship Logs dungeon-pull id, used to correlate back to the report. <c>0</c> for the
    /// implicit pull spanning a dungeon that exposes no dungeon pulls.
    /// </summary>
    public int Id { get; init; }

    /// <summary>The pull's name, as the report states it.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The pull's target shape.</summary>
    public PullKind Targets { get; init; }

    /// <summary>Whether the pull is a boss encounter.</summary>
    public bool IsBoss { get; init; }

    /// <summary>Whether the pull ended in a kill.</summary>
    public bool Kill { get; init; }

    /// <summary>
    /// The event that closes this pull. Assigning it wires <see cref="PullEndEvent.Start"/> back to this
    /// event, so a start and its end are linked by construction.
    /// </summary>
    [JsonIgnore]
    public PullEndEvent End
    {
        get;
        init
        {
            field = value;
            value.Start = this;
        }
    } = null!;

    /// <summary>What the parse produced about this pull, keyed by analyzer surface type.</summary>
    [JsonIgnore]
    public PullMetadata Metadata { get; } = new();

    /// <summary>When the pull starts, in milliseconds.</summary>
    public int StartTime => Timestamp;

    /// <summary>When the pull ends, in milliseconds, read from <see cref="End"/>.</summary>
    public int EndTime => End.Timestamp;

    /// <summary>The pull's duration, in milliseconds.</summary>
    [JsonIgnore]
    public int Duration => EndTime - StartTime;
}
