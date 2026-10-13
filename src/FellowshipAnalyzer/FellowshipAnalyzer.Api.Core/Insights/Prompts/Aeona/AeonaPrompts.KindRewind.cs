namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class AeonaPrompts
{
    /// <summary>Rules of the Kind Rewind guide, after <see cref="Overview"/>.</summary>
    public const string KindRewind = """
        ## Kind Rewind

        Cast Time Shard toward an enemy with Echoes of Ruin active. Each of these casts reduces the cooldown of
        Temporal Barrage.

        Each pull has `timeShardsWithoutEchoesOfRuin`, true when a Time Shard went toward an enemy without Echoes
        of Ruin.

        Patterns and their fixes:

        - `Time Shards without Echoes of Ruin`: apply Echoes of Ruin to the target before the Time Shard.
        """;
}
