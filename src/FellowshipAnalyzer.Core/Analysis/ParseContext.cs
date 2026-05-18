using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Immutable per-analysis context exposing only the values a module reasonably needs at construction
/// time: the selected player, the fight being analyzed, and report-level actor names. Replaces
/// back-reference reads like <c>Owner.PlayerId</c> in module constructors. Resolved per-analysis-run
/// through <c>AnalysisRunServiceProvider</c>.
/// </summary>
public sealed record ParseContext(int PlayerId, ReportFight Fight, IReadOnlyDictionary<int, string> ActorNames)
{
    public int FightStartTime => (int)Fight.StartTime;
    public int FightEndTime => (int)Fight.EndTime;
}
