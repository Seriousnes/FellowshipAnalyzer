using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// Represents a player, NPC, or pet actor in a report.
/// </summary>
public sealed record ReportActor(
    int Id,
    string Name,
    string Type,
    string? SubType,
    string? Server,
    string? Icon
);

/// <summary>
/// Represents a single fight/encounter within a report.
/// </summary>
public sealed record ReportFight(
    int Id,
    string Name,
    int EncounterId,
    bool? Kill,
    double StartTime,
    double EndTime,
    int? Difficulty,
    IReadOnlyList<int>? FriendlyPlayers,
    double? FightPercentage,
    bool InProgress = false
);

/// <summary>
/// Metadata about a combat log report: title, timing, fights, and actors.
/// </summary>
public sealed record ReportInfo(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    IReadOnlyList<ReportFight> Fights,
    IReadOnlyList<ReportActor> Actors
);

/// <summary>
/// Master data for a report: abilities and player actors, cached at the report level.
/// </summary>
public sealed record ReportMasterData(
    IReadOnlyList<Ability> Abilities,
    IReadOnlyList<ReportActor> Actors
);

/// <summary>
/// Combined preload data for analysis: report info and master data fetched in a single API call.
/// </summary>
public sealed record AnalysisPreload(
    ReportInfo ReportInfo,
    ReportMasterData MasterData
);

/// <summary>
/// Represents the deserialized events from a combat log fight, with progress status.
/// </summary>
public sealed record EventsResult(List<Event> Events, bool InProgress);
