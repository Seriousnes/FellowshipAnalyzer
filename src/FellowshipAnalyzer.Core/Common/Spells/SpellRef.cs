using OneOf;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Names the spell an aura or buff query looks for, as either the <see cref="Spell"/> itself or its
/// raw game id <see cref="Spell.Id"/>. Both forms convert implicitly, so a caller writes
/// <c>Spells.EngulfingFlamesDot</c> or <c>Spells.EngulfingFlamesDot.Id</c> and never the namespaced
/// <see cref="FSLID"/> the combat log uses.
/// <para>
/// A <see cref="Spell"/> matches on its <see cref="Spell.FSLID"/>, so an ability and an effect that
/// share a raw id stay distinct. A raw id matches on <see cref="FSLID.NativeId"/>, which is all the
/// caller supplied; prefer the <see cref="Spell"/> form where one is in reach.
/// </para>
/// </summary>
[GenerateOneOf]
public partial class SpellRef : OneOfBase<Spell, int>
{
    /// <summary>Whether <paramref name="candidate"/>, the id from a combat-log event, names this spell.</summary>
    public bool Matches(FSLID candidate)
        => IsT0 ? candidate == AsT0.FSLID : candidate.NativeId == new FSLID(AsT1).NativeId;
}
