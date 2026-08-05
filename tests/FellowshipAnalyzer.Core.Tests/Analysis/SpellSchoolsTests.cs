using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// <see cref="SpellSchools"/> is generated from <c>data/spelldb.json</c>, so a spell's damage school is
/// fixed at compile time and resolves from its <see cref="FSLID"/> alone.
/// </summary>
public sealed class SpellSchoolsTests
{
    private const int PhysicalAbility = 2190;
    private const int MagicEffect = 1_003_005;
    private const int DualSchoolAbility = 255;
    private const int UnclassifiedAbility = 5;

    [Fact]
    public void For_ResolvesBothFslIdRangesAndADualSchoolAbility()
    {
        Assert.Equal(MagicSchool.Physical, SpellSchools.For(new FSLID(PhysicalAbility)));
        Assert.Equal(MagicSchool.Magic, SpellSchools.For(new FSLID(MagicEffect)));
        Assert.Equal(MagicSchool.Magic | MagicSchool.Physical, SpellSchools.For(new FSLID(DualSchoolAbility)));
    }

    [Fact]
    public void For_IsTheDefaultForAnIdTheGameDataDoesNotClassify()
    {
        Assert.Equal(default, SpellSchools.For(new FSLID(UnclassifiedAbility)));
        Assert.Equal(default, SpellSchools.For(new FSLID(2_000_042)));
        Assert.Equal(default, SpellSchools.For(new FSLID(999_999)));
    }
}
