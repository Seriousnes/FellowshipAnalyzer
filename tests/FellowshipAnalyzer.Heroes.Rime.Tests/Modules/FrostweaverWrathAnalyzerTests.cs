using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using RimeTalents = FellowshipAnalyzer.Core.Common.Spells.RimeTalents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Modules;

public sealed class FrostweaverWrathAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;

    private static readonly ReportFight Fight = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 60_000, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task ProcSpentOnASpenderCast_CountsAsConsumed()
    {
        var analyzer = await Analyze(
            ProcApplied(1_000),
            SpenderCast(1_500, Spells.GlacialBlast),
            ProcRemoved(1_500));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);
        analyzer.ProcsOverwritten.ShouldBe(0);
        analyzer.UtilisationShare.ShouldBe(1);
    }

    [Theory]
    [InlineData(1028)]
    [InlineData(1018)]
    [InlineData(2223)]
    [InlineData(2224)]
    public async Task EveryWinterOrbSpender_CanSpendTheProc(int spenderId)
    {
        var spender = new[] { Spells.GlacialBlast, Spells.IceComet, Spells.TalonStrike, Spells.RisingTalons }
            .Single(candidate => candidate.Id == spenderId);

        var analyzer = await Analyze(
            ProcApplied(1_000),
            SpenderCast(1_500, spender),
            ProcRemoved(1_500));

        analyzer.ProcsConsumed.ShouldBe(1);
    }

    [Fact]
    public async Task ProcRemovedWithNoSpenderAgainstIt_CountsAsExpired()
    {
        var analyzer = await Analyze(
            ProcApplied(1_000),
            ProcRemoved(4_000));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
        analyzer.UtilisationShare.ShouldBe(0);
    }

    [Fact]
    public async Task FreshRollOnTopOfABankedProc_CountsAsGainedAndOverwritten()
    {
        var analyzer = await Analyze(
            ProcApplied(1_000),
            ProcRefreshed(2_000),
            SpenderCast(2_500, Spells.IceComet),
            ProcRemoved(2_500));

        analyzer.ProcsGained.ShouldBe(2);
        analyzer.ProcsOverwritten.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);
        analyzer.UtilisationShare.ShouldBe(0.5);
    }

    [Fact]
    public async Task SpenderAlreadyInFlightWhenTheProcRolls_SpendsItOnImpact()
    {
        var analyzer = await Analyze(
            SpenderCast(900, Spells.IceComet),
            ProcApplied(1_000),
            SpenderDamage(1_035, Spells.IceComet),
            ProcRemoved(1_035));

        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);
    }

    [Fact]
    public async Task SpenderDamageLoggedAfterTheRemovalItCaused_StillSpendsTheProc()
    {
        var analyzer = await Analyze(
            SpenderCast(900, Spells.IceComet),
            ProcApplied(1_000),
            ProcRemoved(1_035),
            SpenderDamage(1_035, Spells.IceComet));

        analyzer.ProcsConsumed.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);
    }

    [Fact]
    public async Task SpenderLandingAfterTheRemovalMillisecond_DoesNotRescueTheProc()
    {
        var analyzer = await Analyze(
            ProcApplied(1_000),
            ProcRemoved(1_035),
            SpenderDamage(1_036, Spells.IceComet));

        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task EveryTargetOfAMultiTargetSpender_SpendsTheProcOnlyOnce()
    {
        var analyzer = await Analyze(
            ProcApplied(1_000),
            SpenderCast(1_500, Spells.IceComet),
            SpenderDamage(1_535, Spells.IceComet),
            SpenderDamage(1_535, Spells.IceComet),
            SpenderDamage(1_535, Spells.IceComet),
            ProcRemoved(1_535));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
    }

    [Fact]
    public async Task ProcStillBankedWhenTheFightEnds_IsNeitherSpentNorExpired()
    {
        var analyzer = await Analyze(ProcApplied(1_000));

        analyzer.ProcsGained.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(0);
        analyzer.ProcsOverwritten.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    public async Task ReapplyAfterARemoval_ExpiresTheFirstProcRatherThanOverwritingIt(int reapplyDelayMs)
    {
        var analyzer = await Analyze(
            ProcApplied(1_000),
            ProcRemoved(1_500),
            ProcApplied(1_500 + reapplyDelayMs),
            SpenderCast(2_500, Spells.GlacialBlast),
            ProcRemoved(2_500));

        analyzer.ProcsGained.ShouldBe(2);
        analyzer.ProcsOverwritten.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
        analyzer.ProcsConsumed.ShouldBe(1);
    }

    [Fact]
    public async Task SpenderCastWithNoProcBanked_SpendsNothing()
    {
        var analyzer = await Analyze(
            SpenderCast(1_000, Spells.GlacialBlast),
            SpenderDamage(1_035, Spells.GlacialBlast));

        analyzer.ProcsGained.ShouldBe(0);
        analyzer.ProcsConsumed.ShouldBe(0);
        analyzer.UtilisationShare.ShouldBe(0);
        analyzer.Statistic.ShouldBeNull();
    }

    [Fact]
    public async Task ProcsBanked_ContributeTheStatisticsCard()
    {
        var analyzer = await Analyze(ProcApplied(1_000));

        analyzer.Statistic.ShouldNotBeNull();
    }

    [Fact]
    public async Task WithoutTheTalent_TheModuleNeverActivates()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        await parser.Analyze([Combatant(RimeTalents.Avalanche), ProcApplied(1_000)], PlayerId, Fight);

        parser.GetModule<FrostweaverWrathAnalyzer>().ShouldBeNull();
    }

    private static CombatantInfoEvent Combatant(params int[] nativeTalentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. nativeTalentIds.Select(id => new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, id) })],
    };

    private static ApplyBuffEvent ProcApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ProcAbility,
    };

    private static RefreshBuffEvent ProcRefreshed(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ProcAbility,
    };

    private static RemoveBuffEvent ProcRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ProcAbility,
    };

    private static Ability ProcAbility => new()
    {
        FSLID = Spells.FrostweaversWrathBuff.FSLID,
        Name = Spells.FrostweaversWrathBuff.Name,
    };

    private static CastEvent SpenderCast(int timestamp, Spell spender) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = spender.FSLID, Name = spender.Name },
    };

    private static DamageEvent SpenderDamage(int timestamp, Spell spender) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Amount = 1_000,
        Ability = new Ability { FSLID = spender.FSLID, Name = spender.Name },
    };

    private static async Task<FrostweaverWrathAnalyzer> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        await parser.Analyze([Combatant(RimeTalents.FrostweaversWrath), .. events], PlayerId, Fight);
        return parser.GetModule<FrostweaverWrathAnalyzer>().ShouldNotBeNull();
    }
}
