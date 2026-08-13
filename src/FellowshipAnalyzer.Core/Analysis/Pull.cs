namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A single encounter window within a report-dungeon — the unit of per-encounter analysis.
/// Derived from a Fellowship Logs dungeon pull, or fabricated to span a whole dungeon that exposes
/// no pulls. <see cref="Index"/> is the zero-based dispatch ordinal; <see cref="Id"/> is the
/// Fellowship Logs dungeon-pull id used to correlate back to the report (0 for an implicit
/// whole-dungeon pull). <see cref="StartTime"/> and <see cref="EndTime"/> are milliseconds.
/// </summary>
public sealed record Pull(
    int Index,
    int Id,
    string Name,
    int StartTime,
    int EndTime,
    PullKind Targets,
    bool IsBoss,
    bool Kill)
{
    private readonly Dictionary<Type, Analyzer> _analyzers = [];

    /// <summary>
    /// The <see cref="Analyzer"/> instances retained on this pull, keyed by surface type (the
    /// topmost ancestor deriving directly from <see cref="Analyzer"/>). Populated by the parser
    /// as the pull ends.
    /// </summary>
    public Analyzer? GetAnalyzer(Type surfaceType) =>
        _analyzers.TryGetValue(surfaceType, out var analyzer) ? analyzer : null;

    internal void SetAnalyzer(Type surfaceType, Analyzer analyzer) => _analyzers[surfaceType] = analyzer;
}
