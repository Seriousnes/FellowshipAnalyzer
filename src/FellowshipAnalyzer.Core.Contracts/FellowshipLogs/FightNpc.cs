namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// An enemy NPC participating in a <see cref="ReportFight"/>, as reported by Fellowship Logs.
/// <see cref="InstanceCount"/> is the number of distinct enemy instances seen during the fight,
/// and <see cref="GroupCount"/> the number of distinct instance groups.
/// </summary>
public sealed record FightNpc(
    int? Id,
    int? GameId,
    int? InstanceCount,
    int? GroupCount,
    int? PetOwner
);
