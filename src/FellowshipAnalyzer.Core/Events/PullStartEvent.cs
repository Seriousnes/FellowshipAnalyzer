using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// Marks the start of a <see cref="Analysis.Pull"/> window inside a dungeon. Fabricated by the
/// pull-bookend normalizer; modules hook it via <c>[On&lt;PullStartEvent&gt;]</c>.
/// </summary>
[Fabricated]
public sealed class PullStartEvent : Event
{
    /// <inheritdoc/>
    public override bool? Fabricated => true;

    /// <summary>The pull that is starting.</summary>
    [JsonIgnore]
    public Pull Pull { get; init; } = null!;
}
