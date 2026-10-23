using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using GundeAbilities = FellowshipAnalyzer.Heroes.Gunde.Modules.Abilities;
using Legendaries = FellowshipAnalyzer.Core.Common.Spells.Gunde.Legendaries;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;
using Talents = FellowshipAnalyzer.Core.Common.Spells.Gunde.Talents;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class BloodbathAnalyzerTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int DungeonEnd = 120_000;
    private const int LegendaryQuality = 6;

    [Fact]
    public void BloodbathTalentConstant_IsTalent764()
    {
        GundeTalents.Bloodbath.ShouldBe(764);
        Talents.Bloodbath.Id.ShouldBe(GundeTalents.Bloodbath);
    }

    [Fact]
    public async Task Analyze_WithBloodbath_GrimCarveGivesReignInBloodTwoHundredPercentAccelerationForThreeSeconds()
    {
        var (withTalent, _) = await RunAsync(Scenario(), talents: [Talents.Bloodbath.Id]);
        var (withoutTalent, _) = await RunAsync(Scenario(), talents: []);

        EndCooldown(withoutTalent, Spells.ReignInBlood.FSLID).ShouldBe(90_000);
        EndCooldown(withTalent, Spells.ReignInBlood.FSLID).ShouldBeInRange(83_999, 84_001);
    }

    [Fact]
    public async Task Analyze_WithBloodbath_OtherCooldownsKeepTheirRate()
    {
        var (withTalent, _) = await RunAsync(Scenario(), talents: [Talents.Bloodbath.Id]);
        var (withoutTalent, _) = await RunAsync(Scenario(), talents: []);

        EndCooldown(withTalent, Spells.Rupture.FSLID).ShouldBe(EndCooldown(withoutTalent, Spells.Rupture.FSLID));
    }

    [Fact]
    public async Task Analyze_WithBloodbath_TheWindowClosesThreeSecondsAfterGrimCarve()
    {
        var (parser, _) = await RunAsync(Scenario(), talents: [Talents.Bloodbath.Id]);

        var analyzer = parser.GetModule<BloodbathAnalyzer>().ShouldNotBeNull();
        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.BeginTimestamp.ShouldBe(10_000);
        window.EndTimestamp.ShouldBe(10_000 + BloodbathAnalyzer.DurationMs);

        parser.GetModule<StatTracker>()!
            .CurrentCooldownAcceleration(parser.GetModule<GundeAbilities>()!.GetAbility(Spells.ReignInBlood.FSLID))
            .ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public async Task Analyze_WithBloodbath_ThePlayerHasAThreeSecondBloodbathBuff()
    {
        var (parser, _) = await RunAsync(Scenario(), talents: [Talents.Bloodbath.Id]);

        var buff = parser.SelectedCombatant.Buffs
            .Where(b => b.Ability.FSLID.Value == Talents.Bloodbath.FSLID.Value)
            .ShouldHaveSingleItem();
        buff.Start.ShouldBe(10_000);
        buff.End.ShouldBe(10_000 + BloodbathAnalyzer.DurationMs);
    }

    [Fact]
    public async Task Analyze_WithoutBloodbath_NoAnalyzerRuns()
    {
        var (parser, _) = await RunAsync(Scenario(), talents: []);

        parser.GetModule<BloodbathAnalyzer>().ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_WithBleedingHearts_HeartSplitterTakesTheGrantedChargesAndTimeRateHaste()
    {
        var (parser, _) = await RunAsync([], equipped: [Legendaries.BleedingHearts.Id]);

        var heartSplitter = parser.GetModule<GundeAbilities>()!.GetAbility(Spells.HeartSplitter.FSLID).ShouldNotBeNull();
        heartSplitter.PrimarySpell.Charges.ShouldBe(2);
        heartSplitter.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
    }

    [Fact]
    public async Task Analyze_WithoutBleedingHearts_HeartSplitterKeepsOneChargeAndDurationHaste()
    {
        var (parser, _) = await RunAsync([], equipped: [Legendaries.BloodyMess.Id]);

        var heartSplitter = parser.GetModule<GundeAbilities>()!.GetAbility(Spells.HeartSplitter.FSLID).ShouldNotBeNull();
        heartSplitter.PrimarySpell.Charges.ShouldBe(1);
        heartSplitter.CooldownHaste.ShouldBe(CooldownHasteScaling.Duration);
    }

    private static List<Event> Scenario() =>
    [
        Cast(Spells.ReignInBlood.FSLID, 0),
        Cast(Spells.Rupture.FSLID, 0),
        Cast(Spells.GrimCarve.FSLID, 10_000),
        Cast(Spells.DoubleStrike.FSLID, 100_000),
    ];

    private static int EndCooldown(GundeCombatLogParser parser, int abilityId) =>
        parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Single(e => e.Ability.FSLID.Value == abilityId && e.UpdateType == UpdateSpellUsableType.EndCooldown)
            .Timestamp;

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = abilityId },
        Target = new CastTarget(),
    };

    private static CombatantInfoEvent Combatant(List<int> itemIds, List<int> talentIds) => new()
    {
        SourceId = PlayerId,
        Gear = [.. itemIds.Select(itemId => new Item { Id = itemId, Quality = LegendaryQuality })],
        Talents = [.. talentIds.Select(talentId => new TalentInfo { Id = talentId })],
    };

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> RunAsync(
        List<Event> events, List<int>? equipped = null, List<int>? talents = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var dungeon = new ReportDungeon(0, "Boss", 1, null, 0, DungeonEnd, null, null, null);
        var result = await parser.Analyze([Combatant(equipped ?? [], talents ?? []), .. events], PlayerId, dungeon);
        return (parser, result);
    }

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
