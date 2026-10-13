namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class AeonaPrompts
{
    /// <summary>Rules of the Continuum Shift guide, after <see cref="Overview"/>.</summary>
    public const string ContinuumShift = """
        ## Continuum Shift

        Spend every Continuum Shift on Time Shard. Spend it on Echoes of Ruin only when the tank will die before
        the Time Shard cast completes. With Edge of Ruin, also spend it on Echoes of Ruin when Fleeting Hour
        follows before that Echoes of Ruin expires. Never spend it on Entropy's Claim. Start the enhanced Time
        Shard below half of maximum Chrona, which leaves space for the Chrona it generates.

        Each pull has one entry in `shifts` per Continuum Shift:

        - `spentOn` names the spell that spent it, `nothing` when it ended unspent, or `open at pull end`.
        - On Echoes of Ruin, `tankInDanger` is true when the tank was in danger, and `fleetingHourFollowed` is
          true when Fleeting Hour followed.
        - On Time Shard, `chronaOvercapped` is true when the cast generated Chrona above maximum.

        Patterns and their fixes:

        - `ended unspent` or `spent on Entropy's Claim`: spend every Continuum Shift on Time Shard.
        - `Echoes of Ruin with the tank out of danger`: spend it on Time Shard. This is the costliest pattern
          whenever it shows.
        - `Chrona overcapped`: the Chrona the enhanced Time Shard generated passed maximum Chrona. Start the
          enhanced Time Shard below half of maximum Chrona.

        `spent on Echoes of Ruin` counts every spend on Echoes of Ruin, the right ones included. It is no change on
        its own. While it shows on a kind of pull, never write that every Continuum Shift there went to Time Shard.
        """;
}
