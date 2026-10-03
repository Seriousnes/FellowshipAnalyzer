using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using Xunit;

using MeikoAbilities = FellowshipAnalyzer.Heroes.Meiko.Modules.Abilities;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class MeikoSpellbookTests
{
    public static TheoryData<string> Finishers =>
    [
        nameof(Spells.RisingEarth),
        nameof(Spells.EarthfistBarrage),
        nameof(Spells.RisingStorm),
        nameof(Spells.LashingStormkick),
        nameof(Spells.DoublePalmStrike),
        nameof(Spells.SpiritedVortex),
    ];

    private static readonly Dictionary<string, Spell> ByName = new()
    {
        [nameof(Spells.RisingEarth)] = Spells.RisingEarth,
        [nameof(Spells.EarthfistBarrage)] = Spells.EarthfistBarrage,
        [nameof(Spells.RisingStorm)] = Spells.RisingStorm,
        [nameof(Spells.LashingStormkick)] = Spells.LashingStormkick,
        [nameof(Spells.DoublePalmStrike)] = Spells.DoublePalmStrike,
        [nameof(Spells.SpiritedVortex)] = Spells.SpiritedVortex,
    };

    private static SpellbookAbility Entry(Spell spell) =>
        new MeikoAbilities().Spellbook().Single(entry => entry.PrimarySpell.FSLID == spell.FSLID);

    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new MeikoAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Theory]
    [MemberData(nameof(Finishers))]
    public void Finishers_HaveNoCooldownAndIgnoreTheGlobalCooldown(string finisher)
    {
        var entry = Entry(ByName[finisher]);

        Assert.Null(entry.Gcd);
        Assert.Equal(0, entry.GetCooldown());
    }

    [Fact]
    public void Builders_UseAFixedOneSecondGlobalCooldown()
    {
        foreach (var builder in new Spell[] { Spells.EarthFist, Spells.SpiritPalm, Spells.WindKick })
        {
            var gcd = Entry(builder).Gcd;
            Assert.NotNull(gcd);
            Assert.Null(gcd.Base);
            Assert.NotNull(gcd.Static);
            Assert.Equal(1000.0, gcd.Static.AsT0);
        }
    }

    [Fact]
    public void HasteScaledCooldowns_AreFlagged()
    {
        foreach (var spell in new Spell[] { Spells.StoneShieldAlt, Spells.ShatterEarth, Spells.TwinSoulsBulwark, Spells.Serenity })
            Assert.True(Entry(spell).CooldownReducedByHaste);
    }

    [Fact]
    public void StoneShield_HasTwoThirtySecondCharges()
    {
        var entry = Entry(Spells.StoneShieldAlt);

        Assert.Equal(2, entry.Charges);
        Assert.Equal(30, entry.GetCooldown());
    }
}
