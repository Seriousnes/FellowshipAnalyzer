using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Master data for a report: abilities and player actors, cached at the report level.
/// </summary>
public sealed record ReportMasterData(
    IReadOnlyList<Ability> Abilities,
    IReadOnlyList<ReportActor> Actors
);
