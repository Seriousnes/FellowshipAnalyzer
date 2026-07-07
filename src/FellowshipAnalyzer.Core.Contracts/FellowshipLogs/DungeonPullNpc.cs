namespace FellowshipAnalyzer.Core.FellowshipLogs;

/// <summary>
/// An enemy NPC participating in a <see cref="DungeonPull"/>, as reported by Fellowship Logs.
/// The number of distinct enemy instances is conveyed as the minimum/maximum instance ID and
/// instance group ID ranges observed during the pull.
/// </summary>
public sealed record DungeonPullNpc(
    int? Id,
    int? GameId,
    int? MinimumInstanceId,
    int? MaximumInstanceId,
    int? MinimumInstanceGroupId,
    int? MaximumInstanceGroupId
);
