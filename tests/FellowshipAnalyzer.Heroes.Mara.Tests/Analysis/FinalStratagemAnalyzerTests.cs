using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
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

public sealed class FinalStratagemAnalyzerTests
{
    private const int PlayerId = 7;
    private const int DungeonEnd = 200000;

    [Fact]
    public async Task Analyze_NoStratagemCast_RecordsNothing()
    {
        var analyzer = await AnalyzeAsync([Cast(1000, Spells.QueenFang)]);

        analyzer.Casts.Count.ShouldBe(0);
        analyzer.Casts.ShouldBeEmpty();
        analyzer.CooldownRecoveredMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_StratagemOnAFreshPull_RecoversNothing()
    {
        var analyzer = await AnalyzeAsync([Cast(1000, Spells.FinalStratagem)]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Timestamp.ShouldBe(1000);
        cast.AbilityId.ShouldBe(Spells.FinalStratagem.Id);
        cast.CooldownRecoveredMs.ShouldBe(0);
        cast.AbilitiesReset.ShouldBe(0);
        cast.ChargesRecovered.ShouldBe(0);
        cast.AbilitiesAlreadyAvailable.ShouldBe(cast.Abilities.Count);
    }

    [Fact]
    public async Task Analyze_StratagemAfterSpendingCooldowns_RecordsWhatWasRecharging()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.FinalStratagem),
        };

        var analyzer = await AnalyzeAsync(events);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.AbilitiesReset.ShouldBeGreaterThan(0);
        cast.CooldownRecoveredMs.ShouldBeGreaterThan(0);

        var maiden = cast.Abilities.Single(state => state.AbilityId == Spells.MaidenOfDeath.Id);
        maiden.RemainingMs.ShouldBe(MaidenOfDeathAnalyzer.RechargeMs - 1000);
        maiden.ChargesAvailable.ShouldBe(0);
        maiden.WasAvailable.ShouldBeFalse();
        maiden.ChargesRecovered.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_AbilityStates_AreOrderedByLongestRechargeFirst()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.ShadowWard),
            Cast(3000, Spells.FinalStratagem),
        };

        var analyzer = await AnalyzeAsync(events);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        var remaining = cast.Abilities.Select(state => state.RemainingMs).ToList();
        remaining.ShouldBe([.. remaining.OrderByDescending(value => value)]);
    }

    [Fact]
    public async Task Analyze_ExcludesAbilitiesWithNoCooldownAndTheStratagemItself()
    {
        var analyzer = await AnalyzeAsync([Cast(1000, Spells.FinalStratagem)]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Abilities.ShouldNotContain(state => state.AbilityId == Spells.Attack.Id);
        cast.Abilities.ShouldNotContain(state => state.AbilityId == Spells.FinalStratagem.Id);
        cast.Abilities.ShouldNotContain(state => state.AbilityId == Spells.MacabreStratagem.Id);
        cast.Abilities.ShouldContain(state => state.AbilityId == Spells.MaidenOfDeath.Id);
    }

    [Fact]
    public async Task Analyze_WritesTheResetIntoTheSharedCooldownTracker()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.FinalStratagem),
            Cast(3000, Spells.FinalStratagem),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Casts.Count.ShouldBe(2);

        var before = analyzer.Casts[0].Abilities.Single(state => state.AbilityId == Spells.MaidenOfDeath.Id);
        before.RemainingMs.ShouldBe(MaidenOfDeathAnalyzer.RechargeMs - 1000);
        before.ChargesAvailable.ShouldBe(0);

        var after = analyzer.Casts[1].Abilities.Single(state => state.AbilityId == Spells.MaidenOfDeath.Id);
        after.RemainingMs.ShouldBe(0);
        after.ChargesAvailable.ShouldBe(1);
        after.WasAvailable.ShouldBeTrue();
        after.ChargesRecovered.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_AbilityRecastAfterAReset_GoesOnCooldownAgain()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.FinalStratagem),
            Cast(2500, Spells.MaidenOfDeath),
            Cast(3000, Spells.FinalStratagem),
        };

        var analyzer = await AnalyzeAsync(events);

        var second = analyzer.Casts[1].Abilities.Single(state => state.AbilityId == Spells.MaidenOfDeath.Id);
        second.RemainingMs.ShouldBe(MaidenOfDeathAnalyzer.RechargeMs - 500);
        second.ChargesAvailable.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_WithMacabreStratagemTalented_IsNeverConstructedAndWritesNoReset()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.FinalStratagem),
            Cast(3000, Spells.FinalStratagem),
        };

        var parser = await AnalyzeParserAsync(events, ShortDungeon, withMacabreStratagem: true);

        parser.FinalStratagemAnalyzers.ShouldBeEmpty();

        parser.SpellUsable.ShouldNotBeNull()
            .CooldownRemaining(Spells.MaidenOfDeath.Id, 3000)
            .ShouldBe(MaidenOfDeathAnalyzer.RechargeMs - 2000);
    }

    [Fact]
    public async Task Analyze_WithoutMacabreStratagem_IsActiveAndWritesTheReset()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.FinalStratagem),
        };

        var parser = await AnalyzeParserAsync(events, ShortDungeon);
        var analyzer = parser.FinalStratagemAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.Count.ShouldBe(1);

        parser.SpellUsable.ShouldNotBeNull()
            .CooldownRemaining(Spells.MaidenOfDeath.Id, 3000)
            .ShouldBe(0);
    }

    /// <summary>
    /// Pins the dispatch guarantee the write depends on. <c>EventEmitter.Emit</c> runs dungeon-lifetime
    /// listeners before pull-lifetime ones, so SpellUsable has already begun the Maiden of Death cooldown
    /// when this pull analyzer sees a later cast: the snapshot reads the real remaining recharge rather than
    /// zero, and the reset written afterwards is what the tracker carries from then on.
    /// </summary>
    [Fact]
    public async Task Analyze_SeesCooldownStateWrittenByTheDungeonLifetimeTracker()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(2000, Spells.FinalStratagem),
        };

        var parser = await AnalyzeParserAsync(events, ShortDungeon);
        var analyzer = parser.FinalStratagemAnalyzers.ShouldHaveSingleItem().Analyzer;

        var maiden = analyzer.Casts.ShouldHaveSingleItem()
            .Abilities.Single(state => state.AbilityId == Spells.MaidenOfDeath.Id);

        maiden.RemainingMs.ShouldBe(MaidenOfDeathAnalyzer.RechargeMs - 1000);

        parser.SpellUsable.ShouldNotBeNull()
            .CooldownRemaining(Spells.MaidenOfDeath.Id, 2000)
            .ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var parser = await AnalyzeParserAsync([Cast(1000, Spells.FinalStratagem)], BossDungeon());

        var entry = parser.FinalStratagemAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.FinalStratagemAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).FinalStratagemAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = spell.Id },
    };

    private static CombatantInfoEvent Combatant(bool withMacabreStratagem) => new()
    {
        SourceId = PlayerId,
        Talents = withMacabreStratagem ? [new TalentInfo { Id = MaraTalents.MacabreStratagem }] : [],
    };

    private static ReportDungeon BossDungeon(int endTime = DungeonEnd) =>
        new(0, "", 1, null, 0, endTime, null, null, null);

    /// <summary>
    /// A dungeon that ends before any cooldown started in these fixtures could recharge naturally, so a
    /// post-parse read of the shared tracker reflects what the analyzer wrote rather than a natural expiry.
    /// </summary>
    private static ReportDungeon ShortDungeon => BossDungeon(5000);

    private static async Task<FinalStratagemAnalyzer> AnalyzeAsync(List<Event> events, ReportDungeon? dungeon = null)
    {
        var parser = await AnalyzeParserAsync(events, dungeon ?? BossDungeon());
        return parser.FinalStratagemAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<MaraCombatLogParser> AnalyzeParserAsync(
        List<Event> events, ReportDungeon dungeon, bool withMacabreStratagem = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        await parser.Analyze([Combatant(withMacabreStratagem), .. events], PlayerId, dungeon);
        return parser;
    }
}
