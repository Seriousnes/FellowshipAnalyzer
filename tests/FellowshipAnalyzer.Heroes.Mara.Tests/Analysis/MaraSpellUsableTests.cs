using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaraSpellUsableTests
{
    private const int PlayerId = 7;
    private const int PullStart = 5000;
    private const int PullEnd = 60000;
    private const int DungeonEnd = 70000;
    private const int ShortDungeonEnd = 12000;

    [Fact]
    public async Task Parser_UsesMaraSpellUsable()
    {
        var parser = await AnalyzeAsync([], DungeonEnd);

        parser.SpellUsable.ShouldBeOfType<MaraSpellUsable>();
    }

    [Fact]
    public async Task BroodingShadowsCastBeforeTheFirstPull_StartsNoCooldown()
    {
        var parser = await AnalyzeAsync([Cast(1000, Spells.BroodingShadows)], DungeonEnd);

        parser.SpellUsable.ShouldNotBeNull().ChargesAvailable(Spells.BroodingShadows.FSLID).ShouldBe(2);
        parser.Events.OfType<UpdateSpellUsableEvent>()
            .ShouldNotContain(update => update.Ability.Id == Spells.BroodingShadows.FSLID);
    }

    [Fact]
    public async Task BroodingShadowsCastInsideAPull_SpendsAChargeAndRechargesInFifteenSeconds()
    {
        var beforeRecharge = await AnalyzeAsync([Cast(10000, Spells.BroodingShadows)], ShortDungeonEnd);
        beforeRecharge.SpellUsable.ShouldNotBeNull().ChargesAvailable(Spells.BroodingShadows.FSLID).ShouldBe(1);

        var parser = await AnalyzeAsync([Cast(10000, Spells.BroodingShadows)], DungeonEnd);

        var ended = parser.Events.OfType<UpdateSpellUsableEvent>()
            .Single(update => update.Ability.Id == Spells.BroodingShadows.FSLID
                && update.UpdateType == UpdateSpellUsableType.EndCooldown);
        ended.Timestamp.ShouldBe(25000);
    }

    [Fact]
    public async Task TwoBroodingShadowsCastsInsideAPull_RechargeOneAtATime()
    {
        var parser = await AnalyzeAsync(
            [Cast(10000, Spells.BroodingShadows), Cast(11000, Spells.BroodingShadows)], DungeonEnd);

        parser.Events.OfType<UpdateSpellUsableEvent>()
            .Where(update => update.Ability.Id == Spells.BroodingShadows.FSLID
                && update.UpdateType is UpdateSpellUsableType.RestoreCharge or UpdateSpellUsableType.EndCooldown)
            .Select(update => (update.UpdateType, update.Timestamp))
            .ShouldBe([(UpdateSpellUsableType.RestoreCharge, 25000), (UpdateSpellUsableType.EndCooldown, 40000)]);
    }

    private static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = spell.Id },
    };

    private static async Task<MaraCombatLogParser> AnalyzeAsync(List<Event> events, int dungeonEnd)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var pullEnd = Math.Min(PullEnd, dungeonEnd);
        var dungeon = new ReportDungeon(
            0, "Boss", 1, true, 0, dungeonEnd, null, null, null, false,
            [new DungeonPull(1, 1, true, PullStart, pullEnd, "Boss", null)]);

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        await parser.Analyze([new CombatantInfoEvent { SourceId = PlayerId }, .. events], PlayerId, dungeon);
        return parser;
    }
}
