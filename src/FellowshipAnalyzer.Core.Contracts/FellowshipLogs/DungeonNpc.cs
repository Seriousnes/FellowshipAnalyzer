namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// An enemy NPC participating in a <see cref="ReportDungeon"/>, as reported by Fellowship Logs.
/// <see cref="InstanceCount"/> is the number of distinct enemy instances seen during the dungeon,
/// and <see cref="GroupCount"/> the number of distinct instance groups. Fellowship Logs declares
/// every field nullable; an entry with no <see cref="Id"/> names no actor and is dropped at the API
/// boundary, and the remaining counts default to one.
/// </summary>
public sealed record DungeonNpc(
    int Id,
    int GameId,
    int InstanceCount,
    int GroupCount,
    int? PetOwner
);
