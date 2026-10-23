using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;
using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

/// <summary>
/// Kill Em All (talent 671, effect 3160) gives Heavy Strike +60% Cooldown Acceleration while the effect is
/// active. Heavy Strike's 8-second cooldown therefore recharges in 5 seconds with no Haste.
/// </summary>
public sealed class KillEmAllAnalyzerTests
{
    private const int PlayerId = FuryEconomyAnalyzerTests.PlayerId;
    private const int DungeonEnd = 100_000;
    private const int BaseRechargeMs = 8_000;
    private const int AcceleratedRechargeMs = 5_000;

    private static readonly int KillEmAllId = KillEmAllAnalyzer.KillEmAll.FSLID;
    private static readonly int HeavyStrikeId = TariqSpells.HeavyStrike.FSLID;
    private static readonly int ChainLightningId = TariqSpells.ChainLightning.FSLID;

    [Fact]
    public void TheModel_ReadsTheGameData()
    {
        TariqTalents.KillEmAll.ShouldBe(671);
        KillEmAllAnalyzer.KillEmAll.Id.ShouldBe(3160);
        KillEmAllAnalyzer.HeavyStrikeAcceleration.ShouldBe(0.60);
        TariqSpells.HeavyStrike.Cooldown.ShouldBe(BaseRechargeMs / 1000d);
    }

    [Fact]
    public async Task WhileActive_HeavyStrikeRechargesFaster()
    {
        var parser = await AnalyzeAsync(talented: true,
        [
            Buff<ApplyBuffEvent>(10_000),
            Buff<RemoveBuffEvent>(20_000),
        ]);

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();
        spellUsable.RechargeDurationDuring(HeavyStrikeId, 10_000, 20_000).ShouldBe(AcceleratedRechargeMs);
        spellUsable.RechargeDurationDuring(HeavyStrikeId, 20_000, 30_000).ShouldBe(BaseRechargeMs);
        parser.GetModule<KillEmAllAnalyzer>().ShouldNotBeNull().Windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(10_000, 20_000));
    }

    [Fact]
    public async Task StacksAndRefreshes_KeepOneTerm()
    {
        var parser = await AnalyzeAsync(talented: true,
        [
            Buff<ApplyBuffEvent>(10_000),
            Stack<ApplyBuffStackEvent>(10_000, 2),
            Stack<RemoveBuffStackEvent>(12_000, 1),
            Buff<RefreshBuffEvent>(14_000),
            Buff<RemoveBuffEvent>(20_000),
        ]);

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();
        spellUsable.RechargeDurationDuring(HeavyStrikeId, 10_000, 20_000).ShouldBe(AcceleratedRechargeMs);
        parser.GetModule<KillEmAllAnalyzer>().ShouldNotBeNull().Windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(10_000, 20_000));
    }

    [Fact]
    public async Task AStackWithNoApplication_OpensTheWindow()
    {
        var parser = await AnalyzeAsync(talented: true,
        [
            Stack<ApplyBuffStackEvent>(10_000, 2),
            Buff<RemoveBuffEvent>(20_000),
        ]);

        parser.GetModule<SpellUsable>().ShouldNotBeNull()
            .RechargeDurationDuring(HeavyStrikeId, 10_000, 20_000).ShouldBe(AcceleratedRechargeMs);
    }

    [Fact]
    public async Task ChainLightning_TakesNoTerm()
    {
        var parser = await AnalyzeAsync(talented: true,
        [
            Buff<ApplyBuffEvent>(10_000),
            Buff<RemoveBuffEvent>(20_000),
        ]);

        var spellUsable = parser.GetModule<SpellUsable>().ShouldNotBeNull();
        spellUsable.RechargeDurationDuring(ChainLightningId, 10_000, 20_000)
            .ShouldBe(spellUsable.RechargeDurationDuring(ChainLightningId, 20_000, 30_000));
    }

    [Fact]
    public async Task WithoutTheTalent_HeavyStrikeKeepsItsCooldown()
    {
        var parser = await AnalyzeAsync(talented: false,
        [
            Buff<ApplyBuffEvent>(10_000),
            Buff<RemoveBuffEvent>(20_000),
        ]);

        parser.GetModule<KillEmAllAnalyzer>().ShouldBeNull();
        parser.GetModule<SpellUsable>().ShouldNotBeNull()
            .RechargeDurationDuring(HeavyStrikeId, 10_000, 20_000).ShouldBe(BaseRechargeMs);
    }

    private static TEvent Buff<TEvent>(int timestamp) where TEvent : BuffEvent, new() =>
        FuryEconomyAnalyzerTests.Buff<TEvent>(timestamp, KillEmAllId);

    private static TEvent Stack<TEvent>(int timestamp, int stacks) where TEvent : BuffEvent, IBuffStackEvent, new()
    {
        var stackEvent = Buff<TEvent>(timestamp);
        stackEvent.Stack = stacks;
        return stackEvent;
    }

    private static async Task<TariqCombatLogParser> AnalyzeAsync(bool talented, List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var dungeon = new ReportDungeon(0, "Boss", 1, true, 0, DungeonEnd, null, null, null);
        await parser.Analyze(
            [
                new CombatantInfoEvent
                {
                    SourceId = PlayerId,
                    Talents = talented ? [new TalentInfo { Id = 2_000_000 + TariqTalents.KillEmAll }] : [],
                },
                .. events,
            ],
            playerId: PlayerId,
            dungeon: dungeon);
        return parser;
    }
}
