using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.UI;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Pins <see cref="CooldownModifier.Accelerates"/> to SimFell's <c>Hero.CooldownRate</c>: a Weapon ability's
/// cooldown takes only the recovery its own effects grant (Chronoshift recovers at 8× while channelling it),
/// and every other ability takes each modifier whose scope covers it.
/// </summary>
public sealed class WeaponCooldownAccelerationTests
{
    private static readonly Spell WeaponSpell = new() { Id = 1558, Name = "Weapon", Cooldown = 180, AbilityCategory = AbilityCategory.Weapon };
    private static readonly Spell OtherWeaponSpell = new() { Id = 160, Name = "Other Weapon", Cooldown = 60, AbilityCategory = AbilityCategory.Weapon };
    private static readonly Spell MajorSpell = new() { Id = 10, Name = "Major", Cooldown = 60, AbilityCategory = AbilityCategory.Major };

    private static SpellbookAbility Ability(Spell spell) => new() { PrimarySpell = spell, Category = SpellCategory.Cooldowns };

    [Fact]
    public void UnscopedModifier_SkipsAWeaponAbility() =>
        Assert.False(new CooldownModifier(0.10).Accelerates(Ability(WeaponSpell)));

    [Fact]
    public void UnscopedModifier_AcceleratesAnAbilityOutsideTheWeaponCategory() =>
        Assert.True(new CooldownModifier(0.10).Accelerates(Ability(MajorSpell)));

    [Fact]
    public void ModifierTheWeaponGrants_AcceleratesThatWeapon() =>
        Assert.True(new CooldownModifier(7.0) { GrantedBy = WeaponSpell }.Accelerates(Ability(WeaponSpell)));

    [Fact]
    public void ModifierOneWeaponGrants_SkipsAnotherWeapon() =>
        Assert.False(new CooldownModifier(7.0) { GrantedBy = WeaponSpell }.Accelerates(Ability(OtherWeaponSpell)));

    [Fact]
    public void ModifierAWeaponGrants_AcceleratesEveryOtherAbility() =>
        Assert.True(new CooldownModifier(7.0) { GrantedBy = WeaponSpell }.Accelerates(Ability(MajorSpell)));

    [Fact]
    public void ExportedWeaponSpells_AreWeaponAbilities()
    {
        Assert.Equal(AbilityCategory.Weapon, Items.Chronoshift.AbilityCategory);
        Assert.Equal(AbilityCategory.Weapon, Items.FatedStrike.AbilityCategory);
        Assert.Equal(AbilityCategory.Weapon, Items.TwilightSkybolt.AbilityCategory);
    }

    /// <summary>
    /// Strand of Eternity's 0.10 skips Twilight Skybolt, a Weapon ability. Glorious Purpose's 2.0, granted
    /// by Fated Strike, accelerates Fated Strike.
    /// </summary>
    [Fact]
    public void ExportedWeaponSpells_TakeOnlyTheirOwnModifiers()
    {
        Assert.False(new CooldownModifier(0.10).Accelerates(Ability(Items.TwilightSkybolt)));
        Assert.True(new CooldownModifier(2.0) { GrantedBy = Items.FatedStrike }.Accelerates(Ability(Items.FatedStrike)));
        Assert.True(new CooldownModifier(7.0) { GrantedBy = Items.Chronoshift }.Accelerates(Ability(Items.Chronoshift)));
    }

    /// <summary>
    /// Strand of Eternity's 0.10 and Chronoshift's own 7.0 on Chronoshift: only the 7.0 joins the pool, so
    /// Chronoshift recovers at 8×.
    /// </summary>
    [Fact]
    public void AccelerationTotal_ForAWeapon_SumsOnlyItsOwnModifiers()
    {
        CooldownModifierSet set = [new CooldownModifier(0.10), new CooldownModifier(7.0) { GrantedBy = WeaponSpell }];

        Assert.Equal(7.0, set.AccelerationTotal(Ability(WeaponSpell)), precision: 10);
        Assert.Equal(7.10, set.AccelerationTotal(Ability(MajorSpell)), precision: 10);
    }
}
