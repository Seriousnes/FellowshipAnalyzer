using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class EntropicBurstAnalyzerTests
{
    [Fact]
    public async Task WithoutTheTalent_NoAnalyzerIsConstructed()
    {
        var parser = await Analyze(Info([]),
            Heal(1_000, Spells.EntropyClaim, TankId, 400),
            Damage(1_000, Spells.EntropyClaimDot, 800, tick: true));

        parser.EntropicBurst.ShouldBeNull();
    }

    [Fact]
    public async Task EntropyClaimHeals_AreSplitByTheTickAfterThem()
    {
        var parser = await Analyze(Info([AeonaTalents.EntropicBurst, AeonaTalents.KarmicDispersion]),
            Heal(1_000, Spells.EntropyClaim, TankId, 400),
            Heal(1_000, Spells.EntropyClaim, AllyId, 0, 400),
            Damage(1_000, Spells.EntropyClaimDot, 800, tick: true),
            Heal(1_000, Spells.EntropyClaim, TankId, 300),
            Damage(1_000, Spells.EntropicBurst, 600, SecondEnemyId, tick: true),
            Heal(2_500, Spells.EntropyClaim, TankId, 100, 200),
            Damage(2_501, Spells.EntropicBurst, 600, tick: true));

        var burst = parser.EntropicBurst.ShouldNotBeNull();
        burst.Ticks.ShouldBe(2);
        burst.Damage.ShouldBe(1_200);
        burst.EffectiveHealing.ShouldBe(400);
        burst.Overheal.ShouldBe(200);
        burst.EntropyClaimDamage.ShouldBe(800);
        burst.EntropyClaimEffectiveHealing.ShouldBe(400);
        burst.DamageShare.ShouldBe(0.6, 0.0001);
        burst.HealingShare.ShouldBe(0.5, 0.0001);
        burst.StatisticCategory.ShouldBe(StatisticCategory.Talents);
        burst.Statistic.ShouldNotBeNull();
    }

    private static Task<AeonaCombatLogParser> Analyze(CombatantInfoEvent info, params Event[] events) =>
        AeonaLog.Analyze(BossPull(), [info, .. events]);
}
