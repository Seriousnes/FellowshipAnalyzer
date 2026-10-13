namespace FellowshipAnalyzer.Api.Core.Insights.Prompts;

public static partial class AeonaPrompts
{
    /// <summary>Rules of the Entropy's Claim guide, after <see cref="Overview"/>.</summary>
    public const string EntropyClaim = """
        ## Entropy's Claim

        Cast the next Entropy's Claim 2 to 4 seconds before the active one expires. The next Entropy's Claim
        then expires while Entropic Burst is still active, and the stack rolls over. Keep both charges cycling:
        holding a charge while Entropic Burst lapses loses a stack on every enemy in the pack. Cast Oblivion
        between Entropy's Claim casts instead of a cleanse, so Converging Timelines makes the next charge
        available in time.

        Each pull has `lapsedWithChargeReady`, true when Entropic Burst lapsed with a charge ready to roll it
        over, and one entry in `casts` per Entropy's Claim:

        - `timing` is `in window` inside the 2 to 4 second window, `early` before it, `late` after it, or `none
          active` with no Entropy's Claim running.
        - `rolledOver` is true when its expiry added an Entropic Burst stack.

        Patterns and their fixes:

        - `lapses with a charge ready`: cast the next Entropy's Claim 2 to 4 seconds before the active one expires.
        - `early casts`: the new Entropy's Claim replaced the old one before the stack could roll over. Wait until 2
          to 4 seconds remain.
        - `late casts` or `casts with none active`: keep both charges cycling, and cast Oblivion between Entropy's
          Claim casts.

        A late cast came with under 2 seconds left on the active Entropy's Claim. A cast with none active came with no
        Entropy's Claim running. Neither says how long no Entropy's Claim was running.
        """;
}
