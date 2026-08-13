using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Closes the <see cref="PullStartEvent"/> window it belongs to. Fabricated by
/// <see cref="Analysis.Normalizers.PullBookendNormalizer"/>; modules hook it via
/// <c>[On&lt;PullEndEvent&gt;]</c> and reach the pull's fields, its metadata, and its start instant
/// through <see cref="Start"/>.
/// </summary>
[Fabricated]
public sealed class PullEndEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;

    /// <summary>The pull this event closes. Wired by <see cref="PullStartEvent.End"/>.</summary>
    [JsonIgnore]
    public PullStartEvent Start { get; internal set; } = null!;
}
