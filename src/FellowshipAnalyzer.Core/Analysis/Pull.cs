namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A single encounter window within a report-fight — the unit of per-encounter analysis.
/// Derived from a Fellowship Logs dungeon pull, or fabricated to span a whole fight that exposes
/// no pulls. <see cref="StartTime"/> and <see cref="EndTime"/> are milliseconds.
/// </summary>
public sealed record Pull(
    int Index,
    string Name,
    int StartTime,
    int EndTime,
    PullKind Targets,
    bool IsBoss,
    bool Kill,
    int TargetCount);
