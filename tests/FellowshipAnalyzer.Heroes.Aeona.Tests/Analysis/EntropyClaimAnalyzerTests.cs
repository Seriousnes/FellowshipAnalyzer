using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class EntropyClaimAnalyzerTests
{
    private static readonly int[] Burst = [AeonaTalents.EntropicBurst];

    [Fact]
    public async Task OnlyACompletion_IsACast()
    {
        var analyzer = await Analyze(Info(Burst),
            Activation(1_000, Spells.EntropyClaim),
            Completion(2_500, Spells.EntropyClaim),
            ApplyDebuff(2_500, Spells.EntropyClaimDot));

        analyzer.CastCount.ShouldBe(1);
        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Timestamp.ShouldBe(2_500);
        cast.DotApplied.ShouldBeTrue();
        cast.Target.ShouldBe(new UnitKey(EnemyId, 0));
    }

    [Fact]
    public async Task AnExpiryInsideTheBurstWindow_RollsTheChainOver()
    {
        var analyzer = await Analyze(Info(Burst, AeonaLegendaries.MassEntropy),
            Completion(1_000, Spells.EntropyClaim),
            ApplyDebuff(1_000, Spells.EntropyClaimDot),
            Completion(8_000, Spells.EntropyClaim, SecondEnemyId),
            ApplyDebuff(8_000, Spells.EntropyClaimDot, SecondEnemyId),
            RemoveDebuff(9_000, Spells.EntropyClaimDot),
            ApplyDebuff(9_000, Spells.EntropicBurst),
            ApplyDebuff(9_000, Spells.EntropicBurst, SecondEnemyId),
            RemoveDebuff(16_000, Spells.EntropyClaimDot, SecondEnemyId),
            ApplyDebuffStack(16_000, Spells.EntropicBurst, 2),
            ApplyDebuffStack(16_000, Spells.EntropicBurst, 2, SecondEnemyId));

        analyzer.Rollovers.ShouldBe(1);
        analyzer.Lapses.ShouldBeEmpty();
        analyzer.PeakStacks.ShouldBe(2);
        analyzer.Chains.Count.ShouldBe(2);
        analyzer.Chains.ShouldAllBe(chain => chain.Rollovers == 1 && chain.EndedBy == EntropicBurstChainEnd.PullEnded);

        var second = analyzer.Casts[1];
        second.LeadBeforeExpiryMs.ShouldBe(1_000);
        second.BurstApplications.ShouldBe(0);
        second.BurstRollovers.ShouldBe(2);
        second.RolledOver.ShouldBeTrue();
        analyzer.Casts[0].BurstApplications.ShouldBe(2);
        analyzer.CastsRolledOver.ShouldBe(1);
        analyzer.AverageLeadBeforeExpiryMs!.Value.ShouldBe(1_000.0, 0.0001);
    }

    [Fact]
    public async Task ALapseWithASecondChargeAvailable_IsAMistake()
    {
        var analyzer = await Analyze(Info(Burst, AeonaLegendaries.MassEntropy),
            Completion(1_000, Spells.EntropyClaim),
            ApplyDebuff(1_000, Spells.EntropyClaimDot),
            RemoveDebuff(9_000, Spells.EntropyClaimDot),
            ApplyDebuff(9_000, Spells.EntropicBurst),
            ApplyDebuff(9_000, Spells.EntropicBurst, SecondEnemyId),
            RemoveDebuff(18_000, Spells.EntropicBurst),
            RemoveDebuff(18_050, Spells.EntropicBurst, SecondEnemyId));

        analyzer.RolloverLeadMs.ShouldBe(9_500);
        var lapse = analyzer.Lapses.ShouldHaveSingleItem();
        lapse.Timestamp.ShouldBe(18_000);
        lapse.Units.ShouldBe(2);
        lapse.PeakStacks.ShouldBe(1);
        lapse.ChargeAvailable.ShouldBeTrue();
        analyzer.LapsesWithChargeAvailable.ShouldBe(1);
        analyzer.RolloverShare.ShouldBe(0.0);
        analyzer.Chains.ShouldAllBe(chain => chain.EndedBy == EntropicBurstChainEnd.Lapsed);
    }

    [Fact]
    public async Task ALapseWithNoChargeAvailable_IsNotAMistake()
    {
        var analyzer = await Analyze(Info(Burst),
            Completion(1_000, Spells.EntropyClaim),
            ApplyDebuff(1_000, Spells.EntropyClaimDot),
            RemoveDebuff(7_000, Spells.EntropyClaimDot),
            ApplyDebuff(7_000, Spells.EntropicBurst),
            RemoveDebuff(16_000, Spells.EntropicBurst));

        var lapse = analyzer.Lapses.ShouldHaveSingleItem();
        lapse.ChargeAvailable.ShouldBeFalse();
        analyzer.LapsesWithChargeAvailable.ShouldBe(0);
        analyzer.RolloverShare.ShouldBeNull();
    }

    [Fact]
    public async Task ARemovalAtTheEnemysDeath_EndsTheChainWithoutALapse()
    {
        var analyzer = await Analyze(Info(Burst, AeonaLegendaries.MassEntropy),
            Completion(1_000, Spells.EntropyClaim),
            ApplyDebuff(1_000, Spells.EntropyClaimDot),
            RemoveDebuff(9_000, Spells.EntropyClaimDot),
            ApplyDebuff(9_000, Spells.EntropicBurst),
            Death(12_000, EnemyId),
            RemoveDebuff(12_000, Spells.EntropicBurst));

        analyzer.Lapses.ShouldBeEmpty();
        analyzer.Chains.ShouldHaveSingleItem().EndedBy.ShouldBe(EntropicBurstChainEnd.Died);
    }

    [Fact]
    public async Task EachWaitForACharge_IsMeasured()
    {
        var analyzer = await Analyze(Info([]),
            Completion(5_000, Spells.EntropyClaim),
            ApplyDebuff(5_000, Spells.EntropyClaimDot),
            RemoveDebuff(11_000, Spells.EntropyClaimDot));

        analyzer.Casts.ShouldHaveSingleItem().DelayAfterReadyMs.ShouldBe(5_000);
        analyzer.DelaysAfterReady.Count.ShouldBe(2);
    }

    private static async Task<EntropyClaimAnalyzer> Analyze(CombatantInfoEvent info, params Event[] events)
    {
        var parser = await AeonaLog.Analyze(BossPull(), [info, .. events]);
        return parser.EntropyClaimAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<EntropyClaimAnalyzer>();
    }
}
