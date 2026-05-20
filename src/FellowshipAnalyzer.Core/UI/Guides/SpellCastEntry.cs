using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// A single entry in a <see cref="SpellSequence"/> filmstrip.
/// </summary>
public sealed record SpellCastEntry(
    int Id,
    PerformanceTier? Performance = null,
    string? Tooltip = null);
