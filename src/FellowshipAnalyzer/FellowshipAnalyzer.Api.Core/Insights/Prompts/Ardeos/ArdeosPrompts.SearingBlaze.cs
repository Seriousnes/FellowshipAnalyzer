namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class ArdeosPrompts
{
    /// <summary>Rules of the Searing Blaze guide, after <see cref="Overview"/>.</summary>
    public const string SearingBlaze = """
        ## Searing Blaze

        Searing Blaze never drops off a boss, and goes on every enemy in a pack.

        Each pull has `applied`, true when Searing Blaze landed at least once. A boss has `uptime`, `full`,
        `partial` or `low`, and `droppedOff`, true when Searing Blaze fell off before a reapplication. A pack has
        `spread`, `full`, `partial` or `low` across its enemies.

        Patterns and their fixes:

        - `uptime short of full` or `dropped off`: keep Searing Blaze on the boss the whole fight. Uptime and
          drop-off on the boss are one insight.
        - `spread short of full`: apply Searing Blaze to every enemy in the pack.
        """;
}
