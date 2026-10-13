namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class ArdeosPrompts
{
    /// <summary>Ardeos's rules and example, after <see cref="InsightPrompts.Overview"/>.</summary>
    public const string Overview = """
        ## Ardeos

        The player is an Ardeos. The pattern worth the most is the one costing the most damage.

        Example change: "You spent long stretches at maximum Engulfing Flames charges in trash packs. Spend one
        whenever you are full, then save the next two for the setup before Wildfire."
        """;
}
