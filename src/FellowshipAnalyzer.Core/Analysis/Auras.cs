namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Defines a single aura (buff/debuff) tracked by the analyzer.
/// </summary>
public sealed record SpellbookAura
{
    public required int SpellId { get; init; }

    /// <summary>
    /// When true, this aura is shown on the Timeline aura bar by default.
    /// Analogous to WoWAnalyzer's <c>timelineHighlight</c> property.
    /// </summary>
    public bool TimelineHighlight { get; init; }
}

/// <summary>
/// Per-hero module that declares which auras/buffs are relevant for the timeline.
/// Inherit in each hero's project and override <see cref="GetAuras"/> to register auras.
/// </summary>
public abstract class Auras : Analyzer
{
    private IReadOnlySet<int>? _timelineHighlightedIds;

    /// <summary>Returns all auras registered for this hero.</summary>
    public abstract IEnumerable<SpellbookAura> GetAuras();

    /// <summary>
    /// The set of spell IDs whose auras should appear on the Timeline by default.
    /// Computed once on first access.
    /// </summary>
    public IReadOnlySet<int> TimelineHighlightedIds =>
        _timelineHighlightedIds ??= GetAuras()
            .Where(a => a.TimelineHighlight)
            .Select(a => a.SpellId)
            .ToHashSet();
}
