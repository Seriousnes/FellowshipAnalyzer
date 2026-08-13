using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Marks the end of a <see cref="Analysis.Pull"/> window inside a dungeon. Fabricated by the
/// pull-bookend normalizer; modules hook it via <c>[On&lt;PullEndEvent&gt;]</c>.
/// </summary>
[Fabricated]
public sealed class PullEndEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;

    /// <summary>The pull that just ended.</summary>
    [JsonIgnore]
    public Pull Pull { get; init; } = null!;
}
