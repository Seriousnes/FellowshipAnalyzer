using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Sylvie.Analysis;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

/// <summary>
/// Repository of Frozen Light is in Sylvie's spellbook with Iceshaper's Brand equipped, so its cooldown
/// is tracked and each Frozen Light stack left at expiry takes 0.3 seconds off it.
/// </summary>
public sealed class RepositoryOfFrozenLightTests
{
    private const int CastAt = PullStart + 1_000;
    private const int RemovedAt = CastAt + 31_000;
    private const int Stacks = 15;
    private const int ReductionMs = Stacks * 300;

    [Fact]
    public async Task WithIceshapersBrand_TracksTheCooldownAndAppliesTheFrozenLightReduction()
    {
        var parser = await Run([new Item { Id = CoreItems.IceshapersBrand.Id }]);

        var spellUsable = parser.SpellUsable.ShouldNotBeNull();
        var cooldownMs = (int)(CoreItems.RepositoryOfFrozenLight.Cooldown!.Value * 1000);
        spellUsable.RechargeDuration(CoreItems.RepositoryOfFrozenLight.FSLID).ShouldBe(cooldownMs);
        spellUsable.CooldownRemaining(CoreItems.RepositoryOfFrozenLight.FSLID, RemovedAt)
            .ShouldBe(CastAt + cooldownMs - ReductionMs - RemovedAt);
        parser.GetModule<RepositoryOfFrozenLightAnalyzer>().ShouldNotBeNull().CooldownReduction
            .ShouldBe(new CooldownReductionResult(ReductionMs, ReductionMs));
    }

    [Fact]
    public async Task WithoutIceshapersBrand_LeavesRepositoryOfFrozenLightOutOfTheSpellbook()
    {
        var parser = await Run([]);

        parser.GetModule<CoreAbilities>().ShouldNotBeNull()
            .GetAbility(CoreItems.RepositoryOfFrozenLight.FSLID).ShouldBeNull();
    }

    private static Task<SylvieCombatLogParser> Run(List<Item> gear) =>
        Analyze(
            new CombatantInfoEvent { SourceId = PlayerId, Gear = gear },
            Cast(CastAt, CoreItems.RepositoryOfFrozenLight, PlayerId),
            ApplyBuff(CastAt + 1_000, CoreItems.RepositoryOfFrozenLightBuff, AllyId),
            new ApplyBuffStackEvent
            {
                Timestamp = CastAt + 1_000,
                SourceId = PlayerId,
                TargetId = AllyId,
                Stack = Stacks,
                Ability = new Ability { FSLID = CoreItems.RepositoryOfFrozenLightBuff.FSLID, Name = CoreItems.RepositoryOfFrozenLightBuff.Name },
                AbilityGameId = CoreItems.RepositoryOfFrozenLightBuff.FSLID,
            },
            RemoveBuff(RemovedAt, CoreItems.RepositoryOfFrozenLightBuff, AllyId));
}
