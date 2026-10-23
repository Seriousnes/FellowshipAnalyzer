using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Repository of Frozen Light is in Aeona's spellbook with Fragments of Frozen Time equipped, so its cooldown
/// is tracked and each Frozen Light stack left at expiry takes 0.3 seconds off it.
/// </summary>
public sealed class RepositoryOfFrozenLightTests
{
    private const int CastAt = 2_000;
    private const int RemovedAt = 25_000;
    private const int Stacks = 15;
    private const int ReductionMs = Stacks * 300;

    [Fact]
    public async Task WithFragmentsOfFrozenTime_TracksTheCooldownAndAppliesTheFrozenLightReduction()
    {
        var parser = await Run(Info([], CoreItems.FragmentsOfFrozenTime.Id));

        var spellUsable = parser.SpellUsable.ShouldNotBeNull();
        var cooldownMs = (int)(CoreItems.RepositoryOfFrozenLight.Cooldown!.Value * 1000);
        spellUsable.RechargeDuration(CoreItems.RepositoryOfFrozenLight.FSLID).ShouldBe(cooldownMs);
        spellUsable.CooldownRemaining(CoreItems.RepositoryOfFrozenLight.FSLID, RemovedAt)
            .ShouldBe(CastAt + cooldownMs - ReductionMs - RemovedAt);
        parser.GetModule<RepositoryOfFrozenLightAnalyzer>().ShouldNotBeNull().CooldownReduction
            .ShouldBe(new CooldownReductionResult(ReductionMs, ReductionMs));
    }

    [Fact]
    public async Task WithoutFragmentsOfFrozenTime_LeavesRepositoryOfFrozenLightOutOfTheSpellbook()
    {
        var parser = await Run(Info([]));

        parser.GetModule<CoreAbilities>().ShouldNotBeNull()
            .GetAbility(CoreItems.RepositoryOfFrozenLight.FSLID).ShouldBeNull();
    }

    private static Task<AeonaCombatLogParser> Run(CombatantInfoEvent info) =>
        AeonaLog.Analyze(
            BossPull(),
            info,
            Activation(CastAt, CoreItems.RepositoryOfFrozenLight, PlayerId),
            ApplyBuff(CastAt + 1_000, CoreItems.RepositoryOfFrozenLightBuff, AllyId),
            ApplyBuffStack(CastAt + 1_000, CoreItems.RepositoryOfFrozenLightBuff, Stacks, AllyId),
            RemoveBuff(RemovedAt, CoreItems.RepositoryOfFrozenLightBuff, AllyId));
}
