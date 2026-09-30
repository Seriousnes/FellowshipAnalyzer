using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class KindRewindAnalyzerTests
{
    private static readonly int[] Rewind = [AeonaTalents.KindRewind];

    [Fact]
    public async Task ATimeShardIntoAnEnemyWithEchoesOfRuin_ReducesTemporalBarrage()
    {
        var analyzer = await Analyze(Info(Rewind),
            Activation(500, Spells.TemporalBarrage),
            ApplyDebuff(1_000, Spells.EchoesOfRuinDot),
            Completion(3_000, Spells.TimeShard));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Target.ShouldBe(new UnitKey(EnemyId, 0));
        cast.EchoesOfRuinActive.ShouldBeTrue();
        cast.CooldownReducedMs.ShouldBe(2_000);
        cast.CooldownReductionWastedMs.ShouldBe(0);
        analyzer.CastsWithEchoesOfRuin.ShouldBe(1);
        analyzer.CastsMissed.ShouldBe(0);
        analyzer.MissedShare.ShouldBe(0.0);
        analyzer.CooldownReducedMs.ShouldBe(2_000);
    }

    [Fact]
    public async Task ATimeShardIntoAnEnemyWithoutEchoesOfRuin_IsMissed()
    {
        var analyzer = await Analyze(Info(Rewind),
            Activation(500, Spells.TemporalBarrage),
            ApplyDebuff(1_000, Spells.EchoesOfRuinDot, SecondEnemyId),
            Completion(3_000, Spells.TimeShard, EnemyId));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.EchoesOfRuinActive.ShouldBeFalse();
        cast.CooldownReducedMs.ShouldBe(0);
        analyzer.CastsMissed.ShouldBe(1);
        analyzer.MissedShare.ShouldBe(1.0);
    }

    [Fact]
    public async Task AReductionWhileTemporalBarrageIsAvailable_IsWasted()
    {
        var analyzer = await Analyze(Info(Rewind),
            ApplyDebuff(1_000, Spells.EchoesOfRuinDot),
            Completion(3_000, Spells.TimeShard));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.EchoesOfRuinActive.ShouldBeTrue();
        cast.CooldownReducedMs.ShouldBe(0);
        cast.CooldownReductionWastedMs.ShouldBe(2_000);
        analyzer.CooldownReductionWastedMs.ShouldBe(2_000);
    }

    [Fact]
    public async Task AnExpiredEchoesOfRuin_DoesNotCount()
    {
        var analyzer = await Analyze(Info(Rewind),
            ApplyDebuff(1_000, Spells.EchoesOfRuinDot),
            RemoveDebuff(2_000, Spells.EchoesOfRuinDot),
            Completion(3_000, Spells.TimeShard));

        analyzer.Casts.ShouldHaveSingleItem().EchoesOfRuinActive.ShouldBeFalse();
    }

    [Fact]
    public async Task AnActivationCast_IsNotCounted()
    {
        var analyzer = await Analyze(Info(Rewind),
            ApplyDebuff(1_000, Spells.EchoesOfRuinDot),
            Activation(1_500, Spells.TimeShard),
            Completion(3_000, Spells.TimeShard));

        analyzer.CastCount.ShouldBe(1);
    }

    private static async Task<KindRewindAnalyzer> Analyze(CombatantInfoEvent info, params Event[] events)
    {
        var parser = await AeonaLog.Analyze(BossPull(), [info, .. events]);
        return parser.KindRewindAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<KindRewindAnalyzer>();
    }
}
