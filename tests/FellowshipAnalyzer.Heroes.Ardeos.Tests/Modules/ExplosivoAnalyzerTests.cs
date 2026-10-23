using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class ExplosivoAnalyzerTests
{
    private const int PlayerId = 7;

    [Fact]
    public async Task Explosivo_GivesApocalypseASecondCharge()
    {
        var parser = await AnalyzeAsync([CombatantWithExplosivo(), Cast(Spells.Apocalypse.FSLID, 1000)]);

        parser.SpellUsable!.ChargesAvailable(Spells.Apocalypse.Id).ShouldBe(FellowshipAnalyzer.Heroes.Ardeos.Modules.Abilities.ExplosivoApocalypseCharges - 1);
    }

    [Fact]
    public async Task WithoutExplosivo_ApocalypseHasOneCharge()
    {
        var parser = await AnalyzeAsync([Combatant(), Cast(Spells.Apocalypse.FSLID, 1000)]);

        parser.SpellUsable!.ChargesAvailable(Spells.Apocalypse.Id).ShouldBe(0);
        parser.GetModule<ExplosivoAnalyzer>().ShouldBeNull();
    }

    [Fact]
    public async Task FireBall_WhileApocalypseRecharges_ReducesItsCooldown()
    {
        var parser = await AnalyzeAsync(
        [
            CombatantWithExplosivo(),
            Cast(Spells.Apocalypse.FSLID, 1000),
            Cast(Spells.FireBall.FSLID, 2000),
            Cast(Spells.FireBall.FSLID, 3000),
        ]);

        var analyzer = parser.GetModule<ExplosivoAnalyzer>().ShouldNotBeNull();
        analyzer.CooldownReduction.Total.ShouldBe(2 * ExplosivoAnalyzer.ApocalypseReductionPerFireBallMs);
        analyzer.CooldownReduction.Effective.ShouldBe(2 * ExplosivoAnalyzer.ApocalypseReductionPerFireBallMs);
        analyzer.CooldownReduction.Wasted.ShouldBe(0);
    }

    [Fact]
    public async Task FireBall_WithApocalypseAtFullCharges_WastesTheReduction()
    {
        var parser = await AnalyzeAsync([CombatantWithExplosivo(), Cast(Spells.FireBall.FSLID, 1000)]);

        var analyzer = parser.GetModule<ExplosivoAnalyzer>().ShouldNotBeNull();
        analyzer.CooldownReduction.Total.ShouldBe(ExplosivoAnalyzer.ApocalypseReductionPerFireBallMs);
        analyzer.CooldownReduction.Effective.ShouldBe(0);
        analyzer.CooldownReduction.Wasted.ShouldBe(ExplosivoAnalyzer.ApocalypseReductionPerFireBallMs);
    }

    private static CombatantInfoEvent Combatant() => new() { SourceId = PlayerId };

    private static CombatantInfoEvent CombatantWithExplosivo() => new()
    {
        SourceId = PlayerId,
        Gear = [new Item { Id = Legendaries.Explosivo.Id, Quality = 6 }],
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static async Task<ArdeosCombatLogParser> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        await parser.Analyze(events, PlayerId, new ReportDungeon(0, "", 0, null, 0, 10000, null, null, null));
        return parser;
    }
}
