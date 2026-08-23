using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class AbilityMasterDataNormalizerTests
{
    private const int PhysicalAbility = 2190;
    private const int MagicEffect = 1_003_005;

    private static AbilityMasterDataNormalizer Normalizer(params IEnumerable<Ability> abilities)
    {
        var service = new ReportMasterDataService();
        service.Load(new ReportMasterData([.. abilities], []));
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
    public void Normalize_ResolvesTheExtraAbilityToo()
    {
        var normalizer = Normalizer(new Ability { FSLID = MagicEffect, Name = "Blood Boil" });
        var e = new InterruptEvent { AbilityGameId = PhysicalAbility, ExtraAbilityGameId = MagicEffect };

        normalizer.Normalize([e], playerId: 1);

        Assert.NotNull(e.ExtraAbility);
        Assert.Equal("Blood Boil", e.ExtraAbility.Name);
    }
}
