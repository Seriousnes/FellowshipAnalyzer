using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqAbilities = FellowshipAnalyzer.Heroes.Tariq.Modules.Abilities;
using GameItems = FellowshipAnalyzer.Core.Common.Items.Items;
using TariqLegendaries = FellowshipAnalyzer.Core.Common.Spells.Tariq.Legendaries;
using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;
using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

/// <summary>
/// Kill Em All (talent 671, effect 3160) gives Heavy Strike +160% Cooldown Acceleration while the effect is
/// active. Heavy Strike's 8-second cooldown therefore recharges in 3 077 milliseconds with no Haste.
/// </summary>
public sealed class KillEmAllAnalyzerTests
{
    private const int PlayerId = FuryEconomyAnalyzerTests.PlayerId;
    private const int DungeonEnd = 100_000;
    private const int BaseRechargeMs = 8_000;
    private const int AcceleratedRechargeMs = 3_077;
    private const int LegendaryQuality = 6;

    private static readonly int KillEmAllId = TariqSpells.KillEmAllBuff.FSLID;
    private static readonly int HeavyStrikeId = TariqSpells.HeavyStrike.FSLID;
    private static readonly int ChainLightningId = TariqSpells.ChainLightning.FSLID;

    [Fact]
    public void TheModel_ReadsTheGameData()
    {
        TariqTalents.KillEmAll.ShouldBe(671);
        TariqSpells.KillEmAllBuff.Id.ShouldBe(3160);
        EffectData.CooldownAccelerationsOf(TariqSpells.KillEmAllBuff.Id).ToArray()
            .ShouldBe([new CooldownAccelerationChange(TariqSpells.HeavyStrike.Id, 1.6)]);
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
    public async Task AWindowOpenAtTheEndOfTheLog_GivesHeavyStrikeTheExportedTermAndChainLightningNone()
    {
        var parser = await AnalyzeAsync(talented: true, [Buff<ApplyBuffEvent>(10_000)]);

        var abilities = parser.GetModule<TariqAbilities>().ShouldNotBeNull();
        var statTracker = parser.GetModule<StatTracker>().ShouldNotBeNull();

        statTracker.CurrentCooldownAcceleration(abilities.GetAbility(HeavyStrikeId))
            .ShouldBe(EffectData.CooldownAccelerationsOf(TariqSpells.KillEmAllBuff.Id)[0].Value, 0.0001);
        statTracker.CurrentCooldownAcceleration(abilities.GetAbility(HeavyStrikeId)).ShouldBe(1.6, 0.0001);
        statTracker.CurrentCooldownAcceleration(abilities.GetAbility(ChainLightningId)).ShouldBe(0, 0.0001);
    }

    [Fact]
    public async Task WithALegendaryItem_HeavyStrikeRechargesInTheStrandOfEternityRate()
    {
        var parser = await AnalyzeAsync(talented: true,
        [
            Buff<ApplyBuffEvent>(10_000),
            Buff<RemoveBuffEvent>(20_000),
        ], legendary: true);

        var recovery = EffectModifierValue.CooldownRecoveryOf(GameItems.StrandOfEternity.Id).Value;
        var expected = (int)Math.Round(BaseRechargeMs / (recovery + 1.6));

        expected.ShouldBe(2_963);
        parser.GetModule<SpellUsable>().ShouldNotBeNull()
            .RechargeDurationDuring(HeavyStrikeId, 10_000, 20_000).ShouldBe(expected);
    }

    [Fact]
    public async Task AHeavyStrikeCastBeforeTheBuff_EndsItsCooldownAtTheAcceleratedRate()
    {
        var parser = await AnalyzeAsync(talented: true,
        [
            Cast(0, HeavyStrikeId),
            Buff<ApplyBuffEvent>(2_000),
            Buff<RemoveBuffEvent>(4_000),
            Cast(DungeonEnd - 1, ChainLightningId),
        ]);

        parser.Events.OfType<UpdateSpellUsableEvent>()
            .Single(e => e.Ability.FSLID == HeavyStrikeId && e.UpdateType == UpdateSpellUsableType.EndCooldown)
            .Timestamp.ShouldBe(4_800);
    }

    [Fact]
    public async Task ASkullCrusherCastAtHaste_HasAGlobalCooldownOfOneSecondShortenedByHaste()
    {
        const int hasteRating = 1_000;
        var haste = StatTracker.RatingToPercentage(hasteRating);

        var parser = await AnalyzeAsync(talented: false, [InstantCast(1_000, TariqSpells.SkullCrusher.FSLID)], haste: hasteRating);

        haste.ShouldBeGreaterThan(0.1);
        parser.Events.OfType<GlobalCooldownEvent>().ShouldHaveSingleItem().Duration
            .ShouldBe((int)Math.Round(1_000 / (1 + haste), MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task ASkullCrusherCastAtAboutAQuarterHaste_HasAGlobalCooldownOfOneSecondShortenedByHaste()
    {
        const int hasteRating = 200;
        var haste = StatTracker.RatingToPercentage(hasteRating);

        var parser = await AnalyzeAsync(talented: false, [InstantCast(1_000, TariqSpells.SkullCrusher.FSLID)], haste: hasteRating);

        haste.ShouldBeInRange(0.2, 0.3);
        parser.Events.OfType<GlobalCooldownEvent>().ShouldHaveSingleItem().Duration
            .ShouldBe((int)Math.Round(1_000 / (1 + haste), MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task ACullingStrikeCast_HasNoGlobalCooldown()
    {
        var parser = await AnalyzeAsync(talented: false, [InstantCast(1_000, TariqSpells.CullingStrike.FSLID)]);

        parser.Events.OfType<GlobalCooldownEvent>().ShouldBeEmpty();
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

    private static CastEvent InstantCast(int timestamp, int abilityId)
    {
        var cast = Cast(timestamp, abilityId);
        cast.Channel = null;
        return cast;
    }

    private static CastEvent Cast(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.CastWithoutResources(timestamp, abilityId);

    private static TEvent Buff<TEvent>(int timestamp) where TEvent : BuffEvent, new() =>
        FuryEconomyAnalyzerTests.Buff<TEvent>(timestamp, KillEmAllId);

    private static TEvent Stack<TEvent>(int timestamp, int stacks) where TEvent : BuffEvent, IBuffStackEvent, new()
    {
        var stackEvent = Buff<TEvent>(timestamp);
        stackEvent.Stack = stacks;
        return stackEvent;
    }

    private static async Task<TariqCombatLogParser> AnalyzeAsync(
        bool talented, List<Event> events, bool legendary = false, int haste = 0)
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
                    Haste = haste,
                    Gear = legendary ? [new Item { Id = TariqLegendaries.ExecutionersGrin.Id, Quality = LegendaryQuality }] : [],
                    Talents = talented ? [new TalentInfo { Id = 2_000_000 + TariqTalents.KillEmAll }] : [],
                },
                .. events,
            ],
            playerId: PlayerId,
            dungeon: dungeon);
        return parser;
    }
}
