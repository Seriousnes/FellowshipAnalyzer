using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Core.Common;

/// <summary>
/// One damage-over-time effect a hero maintains, paired with the ability that applies it and how
/// repeat applications combine.
/// </summary>
/// <param name="Cast">The ability the player presses to apply <paramref name="Effect"/>, or
/// <c>null</c> when several abilities apply it and none of them names it.</param>
/// <param name="Effect">The aura that ticks on the target. This is the identity of the effect: it is
/// what the log carries and what <see cref="EffectId"/> reads.</param>
/// <param name="StackMethod">How a fresh application combines with the one already on the
/// target.</param>
public sealed record Dot(Spell? Cast, Spell Effect, StackMethod StackMethod)
{
    /// <summary>The FellowshipLogs id of <see cref="Effect"/>, the id its combat-log events carry.</summary>
    public int EffectId => Effect.FSLID;
}
