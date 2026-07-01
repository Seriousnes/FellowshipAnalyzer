using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// A FellowshipLogs entity id. FSL partitions each entity type into its own "million" range so ids
/// from different types never collide. <see cref="Value"/> is the full namespaced id as it appears
/// on combat-log events; <see cref="NativeId"/> is the native game id with the range offset stripped;
/// <see cref="Kind"/> is the entity type the range identifies.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>Value range</term><description>Kind → NativeId</description></listheader>
///   <item><term>0 – 999,999</term><description>Ability → Value</description></item>
///   <item><term>1,000,000 – 1,999,999</term><description>Effect → Value − 1,000,000</description></item>
///   <item><term>2,000,000 – 2,999,999</term><description>Talent → Value − 2,000,000</description></item>
///   <item><term>3,000,000 +</term><description>Weapon → Value − 3,000,000</description></item>
/// </list>
/// </remarks>
[JsonConverter(typeof(FSLIDJsonConverter))]
public readonly struct FSLID : IEquatable<FSLID>
{
    private const int EffectOffset = 1_000_000;
    private const int TalentOffset = 2_000_000;
    private const int WeaponOffset = 3_000_000;

    public int Value { get; }

    public FSLID(int value) => Value = value;

    public SpellKind Kind => Value switch
    {
        >= WeaponOffset => SpellKind.Weapon,
        >= TalentOffset => SpellKind.Talent,
        >= EffectOffset => SpellKind.Effect,
        _ => SpellKind.Ability,
    };

    public int NativeId => Value switch
    {
        >= WeaponOffset => Value - WeaponOffset,
        >= TalentOffset => Value - TalentOffset,
        >= EffectOffset => Value - EffectOffset,
        _ => Value,
    };

    public static FSLID FromNative(SpellKind kind, int nativeId) => kind switch
    {
        SpellKind.Ability => new FSLID(nativeId),
        SpellKind.Effect => new FSLID(nativeId + EffectOffset),
        SpellKind.Talent => new FSLID(nativeId + TalentOffset),
        SpellKind.Weapon => new FSLID(nativeId + WeaponOffset),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static implicit operator int(FSLID id) => id.Value;
    public static implicit operator FSLID(int value) => new(value);

    public bool Equals(FSLID other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is FSLID other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
