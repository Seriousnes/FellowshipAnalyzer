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
            PrimarySpell = new Spell { Id = 1, Cooldown = 10, CooldownHaste = CooldownHasteScaling.Duration },
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
            PrimarySpell = new Spell { Id = 1, Cooldown = 10, CooldownHaste = CooldownHasteScaling.Duration },
            Category = SpellCategory.Rotational,
            CooldownReducedByHaste = false,
        }.CooldownReducedByHaste.ShouldBeFalse();
}
