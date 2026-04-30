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
/// A character's recent reports as returned by the character API endpoint.
/// </summary>
public sealed record CharacterReports(
    string Name,
    IReadOnlyList<ReportSummary> Reports
);

/// <summary>
/// A brief summary of a report used in character report listings.
/// </summary>
public sealed record ReportSummary(
    string Code,
    string? Title,
    double StartTime,
    double? EndTime,
    int FightCount
);
