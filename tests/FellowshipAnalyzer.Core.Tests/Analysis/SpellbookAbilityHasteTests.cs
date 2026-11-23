using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public class SpellbookAbilityHasteTests
{
    [Fact]
    public void CooldownReducedByHaste_DefaultsToThePrimarySpell() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1, Cooldown = 10, CooldownScaling = new() { DurationHaste = true } },
            Category = SpellCategory.Rotational,
        }.CooldownReducedByHaste.ShouldBeTrue();

    [Fact]
    public void CooldownReducedByHaste_IsFalseForASpellTheExportGivesNoHasteScaling() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1, Cooldown = 10 },
            Category = SpellCategory.Rotational,
        }.CooldownReducedByHaste.ShouldBeFalse();

    [Fact]
    public void CooldownReducedByHaste_SetOnTheAbility_OverridesThePrimarySpell() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1, Cooldown = 10, CooldownScaling = new() { DurationHaste = true } },
            Category = SpellCategory.Rotational,
            CooldownReducedByHaste = false,
        }.CooldownReducedByHaste.ShouldBeFalse();

    [Fact]
    public void CastTime_DividesAHasteScaledCastDurationByHaste() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1, CastDuration = 2.0, CastTimeScalesWithHaste = true },
            Category = SpellCategory.Rotational,
        }.CastTime(0.5).ShouldBe(2.0 / 1.5, 1e-9);

    [Fact]
    public void CastTime_KeepsTheFullChannelDurationUnderHaste() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1027, ChannelDuration = 2.0, ChannelTickInterval = 0.4, CastTimeScalesWithHaste = true },
            Category = SpellCategory.Rotational,
        }.CastTime(0.5).ShouldBe(2.0);

    [Fact]
    public void OnGlobalCooldown_IsTrueForASpellWithAnExportGlobalCooldown() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1, GlobalCooldown = 1.5 },
            Category = SpellCategory.Rotational,
        }.OnGlobalCooldown.ShouldBeTrue();

    [Fact]
    public void OnGlobalCooldown_IsTrueForAnEntryWithGcdAndNoExportValue() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1 },
            Category = SpellCategory.Rotational,
            Gcd = Abilities.StandardGcd,
        }.OnGlobalCooldown.ShouldBeTrue();

    [Fact]
    public void OnGlobalCooldown_IsFalseWithNeither() =>
        new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1 },
            Category = SpellCategory.Rotational,
        }.OnGlobalCooldown.ShouldBeFalse();

    [Fact]
    public void SpellFor_ReturnsTheAdditionalSpellWithThatId()
    {
        var additional = new Spell { Id = 2 };
        var entry = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = 1 },
            AdditionalSpells = [additional],
            Category = SpellCategory.Rotational,
        };

        entry.SpellFor(2).ShouldBeSameAs(additional);
        entry.SpellFor(1).ShouldBeSameAs(entry.PrimarySpell);
    }
}
