namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class ArdeosPrompts
{
    /// <summary>Rules of the Detonate guide, after <see cref="Overview"/>.</summary>
    public const string DetonateEfficiency = """
        ## Detonate

        Cast Detonate with as many DoTs active as possible, or cast it early to avoid capping Embers. With
        Apocalyptic Surge, avoid letting its stacks expire unused.

        Each pull has one entry in `casts` per Detonate cast:

        - `dotStack` is `full`, `partial` or `low`: how many DoT instances each enemy carried.
        - `dotsMissing` names the DoTs on no enemy at the cast.
        - `free` is true when Apocalyptic Surge made the cast free.

        With Apocalyptic Surge, `surgeStacksExpired` is true on a pull where a stack expired unused.

        Patterns and their fixes:

        - `short of a full stack`: apply the missing DoTs to the pack before casting Detonate.
        - `{DoT} missing`: apply {DoT} to the pack before casting Detonate.
        - `expired Apocalyptic Surge stacks`: spend the free Detonate before the stack expires.
        """;
}
