namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A single encounter window within a report-fight — the unit of per-encounter analysis.
/// Derived from a Fellowship Logs dungeon pull, or fabricated to span a whole fight that exposes
/// no pulls. <see cref="Index"/> is the zero-based dispatch ordinal; <see cref="Id"/> is the
/// Fellowship Logs dungeon-pull id used to correlate back to the report (0 for an implicit
/// whole-fight pull). <see cref="StartTime"/> and <see cref="EndTime"/> are milliseconds.
/// </summary>
public sealed record Pull(
    int Index,
    int Id,
    string Name,
    int StartTime,
    int EndTime,
    PullKind Targets,
    bool IsBoss,
    bool Kill,
    int TargetCount)
{
    private readonly Dictionary<Type, IResult> _results = [];

    /// <summary>
    /// The <see cref="Analyzer{TResult}"/> results captured on this pull, keyed by declared result
    /// type. Populated by the parser as the pull ends.
    /// </summary>
    public IResult? GetResult(Type resultType) =>
        _results.TryGetValue(resultType, out var result) ? result : null;

    internal void SetResult(Type resultType, IResult result) => _results[resultType] = result;
}
