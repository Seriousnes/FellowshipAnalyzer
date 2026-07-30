using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class AbilityMasterDataNormalizerTests
{
    private const int PhysicalAbility = 2190;
    private const int MagicEffect = 1_003_005;
    private const int UnclassifiedAbility = 5;

    private static AbilityMasterDataNormalizer Normalizer(params Ability[] abilities)
    {
        var service = new ReportMasterDataService();
        service.Load(new ReportMasterData(abilities, []));
        return new AbilityMasterDataNormalizer(service);
    }

    [Fact]
    public void Normalize_ResolvesAbilityFromMasterData()
    {
        var normalizer = Normalizer(new Ability { FSLID = PhysicalAbility, Name = "Attack" });
        var e = new DamageEvent { AbilityGameId = PhysicalAbility };

        normalizer.Normalize([e], playerId: 1);

        Assert.Equal("Attack", e.Ability.Name);
    }

    [Fact]
    public void Normalize_StampsTheDumpDerivedSchool()
    {
        var normalizer = Normalizer(new Ability { FSLID = PhysicalAbility, Name = "Attack" });
        var physical = new DamageEvent { AbilityGameId = PhysicalAbility };
        var magic = new DamageEvent { AbilityGameId = MagicEffect };

        normalizer.Normalize([physical, magic], playerId: 1);

        Assert.Equal(MagicSchool.Physical, physical.Ability.Type);
        Assert.True(physical.IsPhysical);
        Assert.Equal(MagicSchool.Magic, magic.Ability.Type);
        Assert.False(magic.IsPhysical);
    }

    [Fact]
    public void Normalize_OverwritesASchoolThatArrivedOnAlreadyResolvedMasterData()
    {
        var stale = new Ability { FSLID = PhysicalAbility, Name = "Attack", Type = MagicSchool.Magic };
        var normalizer = Normalizer(stale);
        var e = new DamageEvent { AbilityGameId = PhysicalAbility, Ability = stale };

        normalizer.Normalize([e], playerId: 1);

        Assert.Equal(MagicSchool.Physical, e.Ability.Type);
    }

    [Fact]
    public void Normalize_LeavesAnUnclassifiedAbilityWithNoSchool()
    {
        var normalizer = Normalizer();
        var e = new DamageEvent { AbilityGameId = UnclassifiedAbility };

        normalizer.Normalize([e], playerId: 1);

        Assert.Equal(default, e.Ability.Type);
        Assert.False(e.IsPhysical);
        Assert.False(e.IsMagic);
    }

    [Fact]
    public void Normalize_StampsTheExtraAbilityToo()
    {
        var normalizer = Normalizer();
        var e = new InterruptEvent { AbilityGameId = PhysicalAbility, ExtraAbilityGameId = MagicEffect };

        normalizer.Normalize([e], playerId: 1);

        Assert.NotNull(e.ExtraAbility);
        Assert.Equal(MagicSchool.Magic, e.ExtraAbility.Type);
    }

    [Fact]
    public void SpellSchools_ResolvesBothFslIdRangesAndADualSchoolAbility()
    {
        Assert.Equal(MagicSchool.Physical, SpellSchools.For(new FSLID(PhysicalAbility)));
        Assert.Equal(MagicSchool.Magic, SpellSchools.For(new FSLID(MagicEffect)));
        Assert.Equal(MagicSchool.Magic | MagicSchool.Physical, SpellSchools.For(new FSLID(255)));
        Assert.Equal(default, SpellSchools.For(new FSLID(UnclassifiedAbility)));
        Assert.Equal(default, SpellSchools.For(new FSLID(2_000_042)));
        Assert.Equal(default, SpellSchools.For(new FSLID(999_999)));
    }
}
