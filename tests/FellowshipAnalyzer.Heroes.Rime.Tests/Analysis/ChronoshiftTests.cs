using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Rime.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;
using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Analysis;

/// <summary>
/// Chronoshift is in Rime's spellbook with Asha's Chronoshift Spire equipped, so its cooldown is tracked and
/// recovers at the normal rate while it is channelled, because its own cooldown scales off Ability Cooldown
/// Reduction alone.
/// </summary>
public sealed class ChronoshiftTests
{
    private const int PlayerId = 7;
    private const int CastAt = 1_000;
    private const int ChannelAt = 2_000;
    private const int ReadAt = 20_000;
    private const int DungeonEnd = 30_000;

    [Fact]
    public async Task WithAshasChronoshiftSpire_TracksTheCooldownAtTheNormalRateDuringTheChannel()
    {
        var parser = await Run([new Item { Id = CoreItems.AshasChronoshiftSpire_Rime.Id }]);

        var spellUsable = parser.SpellUsable.ShouldNotBeNull();
        var cooldownMs = (int)(CoreItems.Chronoshift.Cooldown!.Value * 1000);
        spellUsable.RechargeDuration(CoreItems.Chronoshift.FSLID).ShouldBe(cooldownMs);
        ShouldBeNear(
            spellUsable.CooldownRemaining(CoreItems.Chronoshift.FSLID, ReadAt),
            CastAt + cooldownMs - ReadAt);
        parser.GetModule<ChronoshiftAnalyzer>().ShouldNotBeNull().RecoveryByAbility
            .ShouldNotContain(r => r.SpellId == CoreItems.Chronoshift.FSLID);
    }

    [Fact]
    public async Task AChronoshiftCast_HasTheSpellsGlobalCooldownAtZeroHaste()
    {
        var cast = new CastEvent { Timestamp = CastAt, SourceId = PlayerId, TargetId = -1, Activation = true, Ability = Chronoshift() };

        await Run([new Item { Id = CoreItems.AshasChronoshiftSpire_Rime.Id }], cast);

        var globalCooldown = cast.GlobalCooldown.ShouldNotBeNull();
        globalCooldown.Duration.ShouldBe((int)(CoreItems.Chronoshift.GlobalCooldown!.Value * 1000));
    }

    [Fact]
    public async Task WithoutAshasChronoshiftSpire_LeavesChronoshiftOutOfTheSpellbook()
    {
        var parser = await Run([]);

        parser.GetModule<CoreAbilities>().ShouldNotBeNull()
            .GetAbility(CoreItems.Chronoshift.FSLID).ShouldBeNull();
    }

    private static void ShouldBeNear(int actual, int expected) =>
        Math.Abs(actual - expected).ShouldBeLessThanOrEqualTo(2, $"{actual} vs {expected}");

    private static SpellAbility Chronoshift() => new()
    {
        FSLID = CoreItems.Chronoshift.FSLID,
        Name = CoreItems.Chronoshift.Name,
    };

    private static async Task<RimeCombatLogParser> Run(List<Item> gear, CastEvent? cast = null)
    {
        cast ??= new CastEvent { Timestamp = CastAt, SourceId = PlayerId, TargetId = -1, Activation = true, Ability = Chronoshift() };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        var dungeon = new ReportDungeon(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: DungeonEnd, Difficulty: null, FriendlyPlayers: null, CompletionPercentage: null);
        await parser.Analyze(
        [
            new CombatantInfoEvent { Timestamp = 0, SourceId = PlayerId, Gear = gear },
            cast,
            new BeginChannelEvent { Timestamp = ChannelAt, SourceId = PlayerId, Ability = Chronoshift() },
        ], PlayerId, dungeon);
        return parser;
    }
}
