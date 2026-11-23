using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Pins the recovery rate of a Weapon ability to the attributes its <see cref="CooldownScaling"/> names: a
/// Weapon ability takes Cooldown Recovery only when its time rate scales off it, takes Haste only where its
/// scaling places Haste, and takes every Cooldown Acceleration term whose scope covers it, as every other ability does.
/// </summary>
public sealed class WeaponCooldownAccelerationTests
{
    private static readonly Spell MajorSpell = new() { Id = 10, Name = "Major", Cooldown = 60, AbilityCategory = AbilityCategory.Major };

    private static SpellbookAbility Ability(Spell spell) => new() { PrimarySpell = spell, Category = SpellCategory.Cooldowns };

    [Fact]
    public void ExportedWeaponSpells_AreWeaponAbilities()
    {
        Assert.Equal(AbilityCategory.Weapon, Items.Chronoshift.AbilityCategory);
        Assert.Equal(AbilityCategory.Weapon, Items.FatedStrike.AbilityCategory);
        Assert.Equal(AbilityCategory.Weapon, Items.TwilightSkybolt.AbilityCategory);
    }

    [Fact]
    public void ChronoshiftAndFatedStrike_ScaleTheirDurationOffCooldownReductionOnly()
    {
        Assert.Equal(new CooldownScaling { DurationCooldownReduction = true }, Items.Chronoshift.CooldownScaling);
        Assert.Equal(new CooldownScaling { DurationCooldownReduction = true }, Items.FatedStrike.CooldownScaling);
    }

    [Fact]
    public void TwilightSkybolt_ScalesItsTimeRateOffHaste()
    {
        Assert.Equal(new CooldownScaling { TimeRateHaste = true }, Items.TwilightSkybolt.CooldownScaling);
    }

    [Fact]
    public void UnscopedModifier_CoversAWeaponAbility() =>
        Assert.True(new CooldownModifier(0.10).Covers(Ability(StatTrackerCooldownTests.WeaponStandardSpell)));

    [Fact]
    public void ScopedModifier_CoversOnlyItsScope()
    {
        var modifier = new CooldownModifier(7.0, new Spell[] { StatTrackerCooldownTests.WeaponStandardSpell });

        Assert.True(modifier.Covers(Ability(StatTrackerCooldownTests.WeaponStandardSpell)));
        Assert.False(modifier.Covers(Ability(StatTrackerCooldownTests.WeaponHastedSpell)));
    }

    [Fact]
    public void AccelerationTotal_SumsEveryEntryCoveringTheAbility()
    {
        CooldownModifierSet set =
        [
            new CooldownModifier(0.10),
            new CooldownModifier(7.0, new Spell[] { StatTrackerCooldownTests.WeaponStandardSpell }),
        ];

        Assert.Equal(7.10, set.AccelerationTotal(Ability(StatTrackerCooldownTests.WeaponStandardSpell)), precision: 10);
        Assert.Equal(0.10, set.AccelerationTotal(Ability(MajorSpell)), precision: 10);
    }

    [Fact]
    public async Task WeaponAbility_TakesAnUnscopedAccelerationTerm()
    {
        var (statTracker, spellUsable, _) = await StatTrackerCooldownTests.Run();
        statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, new CooldownModifier(0.10));

        Assert.Equal(1.10, spellUsable.EffectiveRate(StatTrackerCooldownTests.WeaponStandardSpell.Id), precision: 6);
    }

    [Fact]
    public async Task WeaponAbility_TakesNoCooldownRecoveryWhenItsTimeRateDoesNotScaleOffIt()
    {
        var (statTracker, spellUsable, _) = await StatTrackerCooldownTests.Run(
            cooldownRecoverySeed: [new CooldownModifier(1.1)]);
        statTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, new CooldownModifier(8.0));

        Assert.Equal(1.0, spellUsable.EffectiveRate(StatTrackerCooldownTests.WeaponStandardSpell.Id), precision: 6);
        Assert.Equal(1.0, spellUsable.EffectiveRate(StatTrackerCooldownTests.WeaponHastedSpell.Id), precision: 6);
    }

    [Fact]
    public async Task WeaponAbility_TakesHasteOnlyWhereItsScalingPlacesHaste()
    {
        var (statTracker, spellUsable, _) = await StatTrackerCooldownTests.Run(events: [new DungeonStartEvent { Timestamp = 0 }]);
        statTracker.SetPassiveBonus(this, new PassiveStatBonus { Haste = 0.3 });

        Assert.Equal(1.3, spellUsable.EffectiveRate(StatTrackerCooldownTests.WeaponHastedSpell.Id), precision: 6);
        Assert.Equal(1.0, spellUsable.EffectiveRate(StatTrackerCooldownTests.WeaponStandardSpell.Id), precision: 6);
    }

    [Fact]
    public async Task WeaponAbility_TakesAcrWhenItsDurationScalesOffIt()
    {
        var (statTracker, spellUsable, _) = await StatTrackerCooldownTests.Run();
        statTracker.AddCooldownModifier(CooldownPool.AbilityCooldownReduction, new CooldownModifier(0.12));

        Assert.Equal(158_400, spellUsable.RechargeDuration(StatTrackerCooldownTests.WeaponStandardSpell.Id));
        Assert.Equal(20_000, spellUsable.RechargeDuration(StatTrackerCooldownTests.WeaponHastedSpell.Id));
    }
}
