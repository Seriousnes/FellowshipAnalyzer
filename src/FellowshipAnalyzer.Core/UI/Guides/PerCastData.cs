using Microsoft.AspNetCore.Components;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// Optional rich content block rendered below the stat tiles and sequence filmstrip in
/// <see cref="CastDetail"/>.
/// </summary>
/// <param name="Title">Heading shown above <paramref name="Content"/>; omitted when null or blank.</param>
/// <param name="Content">The additional markup to render.</param>
public sealed record AdditionalContent(string? Title, RenderFragment Content);

/// <summary>
/// Complete data for displaying a single cast in a <see cref="CastDetail"/> inspector.
/// </summary>
public sealed class PerCastData
{
    /// <summary>
    /// The tier used to colour the accent bar and stat tiles for this cast. <c>null</c> when the
    /// segment states no verdict over this row, which colours it neutrally and leaves it in view
    /// under every performance filter.
    /// </summary>
    public required QualitativePerformance? Performance { get; init; }

    /// <summary>The stat tiles shown for this cast.</summary>
    public required PerCastStat[] Stats { get; init; }

    /// <summary>The formatted display timestamp for this cast, as produced by <see cref="CombatLogParser.FormatTimestamp"/>.</summary>
    public required string Timestamp { get; init; }

    /// <summary>Optional tooltip text shown for this cast.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Optional extra markup rendered in the details box below the cast card.</summary>
    public RenderFragment? Details { get; init; }

    /// <summary>Optional rich content block rendered below the stat tiles and sequence filmstrip.</summary>
    public AdditionalContent? AdditionalContent { get; init; }

    /// <summary>
    /// Optional spell sequence entries to display in a <see cref="SpellSequence"/> filmstrip.
    /// </summary>
    public SpellCastEntry[]? Sequence { get; init; }

    /// <summary>
    /// Optional grouping key/label (e.g. a pull name). Shown on the cast card when
    /// <see cref="GroupContent"/> is not supplied.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Optional rich banner for this cast's group, rendered above the stat tiles (e.g. a pull
    /// identifier). Takes precedence over <see cref="Group"/>.
    /// </summary>
    public RenderFragment? GroupContent { get; init; }
}
