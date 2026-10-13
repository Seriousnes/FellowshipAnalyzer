namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class AeonaPrompts
{
    /// <summary>Aeona's rules and example, after <see cref="InsightPrompts.Overview"/>.</summary>
    public const string Overview = """
        ## Aeona

        The player is an Aeona healer. The pattern worth the most is the one costing the most healing or damage.

        Example change: "You spent Continuum Shift on Echoes of Ruin while the tank was in no danger. Spend it on
        Time Shard instead, and keep Echoes of Ruin for a tank who would die before the Time Shard cast completes."
        """;
}
