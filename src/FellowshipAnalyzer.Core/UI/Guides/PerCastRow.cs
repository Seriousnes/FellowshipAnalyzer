using Microsoft.AspNetCore.Components;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// The per-row fields a guide supplies when projecting analyzers into <see cref="PerCastData"/> via
/// the <c>ToPullRows</c> / <c>ToItemRows</c> extensions. The pull-derived grouping (timestamp label,
/// group name, and the <see cref="PullBanner"/>) is filled in by the projection.
/// </summary>
public sealed record PerCastRow
{
    public required PerformanceTier Performance { get; init; }
    public required PerCastStat[] Stats { get; init; }

    /// <summary>Absolute event timestamp (ms) for this row; defaults to the pull's start time.</summary>
    public int? Timestamp { get; init; }

    public SpellCastEntry[]? Sequence { get; init; }
    public AdditionalContent? AdditionalContent { get; init; }
    public RenderFragment? Details { get; init; }
    public string? Tooltip { get; init; }
}
