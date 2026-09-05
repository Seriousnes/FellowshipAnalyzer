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
    /// <summary>
    /// The tier used to colour the accent bar and stat tiles for this cast. <c>null</c> when the
    /// segment states no verdict over this row, which colours it neutrally and leaves it in view
    /// under every performance filter.
    /// </summary>
    public required QualitativePerformance? Performance { get; init; }

    /// <summary>The stat tiles shown for this cast.</summary>
    public required PerCastStat[] Stats { get; init; }

    /// <summary>Absolute event timestamp (ms) for this row; defaults to the pull's start time.</summary>
    public int? Timestamp { get; init; }

    /// <summary>Optional spell sequence entries to display in a <see cref="SpellSequence"/> filmstrip.</summary>
    public SpellCastEntry[]? Sequence { get; init; }

    /// <summary>Optional rich content block rendered below the stat tiles and sequence filmstrip.</summary>
    public AdditionalContent? AdditionalContent { get; init; }

    /// <summary>Optional extra markup rendered in the details box below the cast card.</summary>
    public RenderFragment? Details { get; init; }

    /// <summary>Optional tooltip text shown for this cast.</summary>
    public string? Tooltip { get; init; }
}
