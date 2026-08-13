using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class ReignOfFireAnalyzerTests
{
    private const int PlayerId = 7;
    private const int ReignOfFireTalentId = 157;

    [Fact]
    public void FireBall_IsCuratedAsTwoChargeThirtySecondSpell()
    {
        Spells.FireBall.Charges.ShouldBe(2);
        Spells.FireBall.Cooldown.ShouldBe(30d);
    }

    [Fact]
    public async Task FireBall_ChargesAreModelledBySpellUsable()
    {
        var parser = await AnalyzeAsync([FireBallCast(0), FireBallCast(100)]);

        var updates = parser.Events.OfType<UpdateSpellUsableEvent>()
            .Where(e => e.Ability.Id == Spells.FireBall.Id)
            .ToList();
        updates.ShouldContain(e => e.UpdateType == UpdateSpellUsableType.UseCharge && e.ChargesAvailable == 0);
    }

    [Fact]
    public async Task FireBall_RechargeIsThirtySecondsAtZeroHaste()
    {
        var parser = await AnalyzeAsync([FireBallCast(0)]);

        var begin = parser.Events.OfType<UpdateSpellUsableEvent>()
            .First(e => e.Ability.Id == Spells.FireBall.Id && e.UpdateType == UpdateSpellUsableType.BeginCooldown);
        begin.ExpectedRechargeDuration.ShouldBe(30000);
    }

    [Fact]
    public async Task ReignOfFireProc_RestoresAFireBallCharge()
    {
        var parser = await AnalyzeAsync(
        [
            CombatantWithReignOfFire(),
            FireBallCast(0),
            ReignOfFireProc(100),
        ]);

        parser.GetModule<SpellUsable>().ShouldNotBeNull()
            .ChargesAvailable(Spells.FireBall.Id).ShouldBe(2);

        var analyzer = parser.GetModule<ReignOfFireAnalyzer>().ShouldNotBeNull();
        analyzer.Procs.ShouldBe(1);
        analyzer.ChargesWasted.ShouldBe(0);
    }

    [Fact]
    public async Task ReignOfFireProc_AtMaxCharges_IsWasted()
    {
        var parser = await AnalyzeAsync(
        [
            CombatantWithReignOfFire(),
            ReignOfFireProc(0),
        ]);

        var analyzer = parser.GetModule<ReignOfFireAnalyzer>().ShouldNotBeNull();
        analyzer.Procs.ShouldBe(1);
        analyzer.ChargesWasted.ShouldBe(1);
    }

    [Fact]
    public async Task ReignOfFireBuff_ExpiredWithoutCast_IsWasted()
    {
        var parser = await AnalyzeAsync(
        [
            CombatantWithReignOfFire(),
            FireBallCast(0),
            ReignOfFireProc(50),
            ReignOfFireRemoved(5000),
        ]);

        parser.GetModule<ReignOfFireAnalyzer>().ShouldNotBeNull()
            .EmpowermentsWasted.ShouldBe(1);
    }

    [Fact]
    public async Task ReignOfFireBuff_ConsumedAtCast_IsNotWasted()
    {
        var parser = await AnalyzeAsync(
        [
            CombatantWithReignOfFire(),
            FireBallCast(0),
            ReignOfFireProc(50),
            FireBallCast(1000),
            ReignOfFireRemoved(1000),
        ]);

        var analyzer = parser.GetModule<ReignOfFireAnalyzer>().ShouldNotBeNull();
        analyzer.EmpowermentsWasted.ShouldBe(0);
        analyzer.ChargesWasted.ShouldBe(0);
    }

    [Fact]
    public async Task ReignOfFire_WithoutTalent_IsInactive()
    {
        var parser = await AnalyzeAsync([ReignOfFireProc(0)]);

        parser.GetModule<ReignOfFireAnalyzer>().ShouldBeNull();
    }

    private static CombatantInfoEvent CombatantWithReignOfFire() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = ReignOfFireTalentId }],
    };

    private static CastEvent FireBallCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = Spells.FireBall.FSLID },
    };

    private static ApplyBuffEvent ReignOfFireProc(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ReignOfFireBuff.FSLID },
    };

    private static RemoveBuffEvent ReignOfFireRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ReignOfFireBuff.FSLID },
    };

    private static async Task<ArdeosCombatLogParser> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        await parser.Analyze(events, PlayerId, new ReportDungeon(0, "", 0, null, 0, 6000, null, null, null));
        return parser;
    }
}
