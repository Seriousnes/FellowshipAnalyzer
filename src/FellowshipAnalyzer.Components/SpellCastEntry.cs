namespace FellowshipAnalyzer.Components;

/// <summary>
/// A single entry in a <see cref="SpellSequence"/> filmstrip.
/// </summary>
public sealed record SpellCastEntry(
    int SpellId,
    PerformanceTier? Performance = null,
    string? Tooltip = null);
