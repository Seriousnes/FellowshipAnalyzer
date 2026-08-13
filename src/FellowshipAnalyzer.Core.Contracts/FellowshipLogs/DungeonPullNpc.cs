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
)
{
    /// <summary>The first spawn instance this entry names, defaulting to the sole instance.</summary>
    public int LowInstance => MinimumInstanceId ?? 1;

    /// <summary>
    /// The last spawn instance this entry names. An entry naming a range states both bounds, so one
    /// stated alone, or a maximum below the minimum, names a single instance at
    /// <see cref="LowInstance"/>.
    /// </summary>
    public int HighInstance =>
        MinimumInstanceId is int low && MaximumInstanceId is int high && high >= low ? high : LowInstance;
}
