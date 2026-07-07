using Microsoft.AspNetCore.Components;

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

public sealed record AdditionalContent(string? Title, RenderFragment Content);

/// <summary>
/// Complete data for displaying a single cast in a <see cref="CastDetail"/> inspector.
/// </summary>
public sealed class PerCastData
{
    public required PerformanceTier Performance { get; init; }
    public required PerCastStat[] Stats { get; init; }
    public required string Timestamp { get; init; }
    public string? Tooltip { get; init; }
    public RenderFragment? Details { get; init; }
    public AdditionalContent? AdditionalContent { get; init; }

    /// <summary>
    /// Optional spell sequence entries to display in a <see cref="SpellSequence"/> filmstrip.
    /// </summary>
    public SpellCastEntry[]? Sequence { get; init; }
}
