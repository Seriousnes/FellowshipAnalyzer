using FellowshipAnalyzer.SpellData.Model;

namespace FellowshipAnalyzer.SpellData;

/// <summary>Decodes and encodes FSL id-range offsets for each <see cref="SpellKind"/>.</summary>
public static class SpellKindRange
{
    private const int EffectOffset = 1_000_000;
    private const int TalentOffset = 2_000_000;
    private const int WeaponOffset = 3_000_000;

    public static SpellKind FromFslId(int fslId) => fslId switch
    {
        < EffectOffset => SpellKind.Ability,
        < TalentOffset => SpellKind.Effect,
        < WeaponOffset => SpellKind.Talent,
        _ => SpellKind.Weapon,
    };

    public static int NativeId(int fslId) => fslId switch
    {
        < EffectOffset => fslId,
        < TalentOffset => fslId - EffectOffset,
        < WeaponOffset => fslId - TalentOffset,
        _ => fslId - WeaponOffset,
    };

    public static int GuidFor(SpellKind kind, int nativeId) => kind switch
    {
        SpellKind.Ability => nativeId,
        SpellKind.Effect => nativeId + EffectOffset,
        SpellKind.Talent => nativeId + TalentOffset,
        SpellKind.Weapon => nativeId + WeaponOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
