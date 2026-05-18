using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Immutable per-analysis context exposing only the values a module reasonably needs at construction
/// time: the selected player, the fight being analyzed, and report-level actor names. Replaces
/// back-reference reads like <c>Owner.PlayerId</c> in module constructors (§3 of the redesign doc).
/// </summary>
/// <remarks>
/// Resolved per-analysis-run through <c>AnalysisRunServiceProvider</c>. Modules that need a value
/// at ctor time take it as a parameter; modules that only need it during event handling can
/// keep reading from <see cref="Module.Owner"/>.
/// </remarks>
public sealed record ParseContext(int PlayerId, ReportFight Fight, IReadOnlyDictionary<int, string> ActorNames)
{
    public int FightStartTime => (int)Fight.StartTime;
    public int FightEndTime => (int)Fight.EndTime;
}
