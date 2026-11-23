using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using ArdeosAbilities = FellowshipAnalyzer.Heroes.Ardeos.Modules.Abilities;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Ardeos.Spells;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Analysis;

public sealed class ArdeosSpellbookTests
{
    private const int PlayerId = 7;
    private const int HasteRating = 1000;
    private const int GlobalCooldownMs = 1000;

    [Fact]
    public void GlobalCooldown_ComesFromTheSpellRecord()
    {
        var spellbook = new ArdeosAbilities().Spellbook().ToList();

        SpellbookAbility Entry(Spell spell) => spellbook.Single(e => e.PrimarySpell.FSLID == spell.FSLID);

        spellbook.ShouldAllBe(e => e.Gcd == null);

        Entry(Spells.Detonate).OnGlobalCooldown.ShouldBeTrue();
        Entry(Spells.Incinerate).OnGlobalCooldown.ShouldBeTrue();
        Entry(Spells.Wildfire).OnGlobalCooldown.ShouldBeFalse();
        Entry(Spells.Pyromania).OnGlobalCooldown.ShouldBeFalse();
    }

    [Fact]
    public async Task Detonate_HasAThousandMillisecondGlobalCooldownHasteDoesNotShorten()
    {
        var parser = await AnalyzeAsync([Combatant(), Cast(Spells.Detonate.FSLID, 1000)]);

        parser.Events.OfType<CastEvent>().Single().GlobalCooldown.ShouldNotBeNull().Duration.ShouldBe(GlobalCooldownMs);
    }

    [Fact]
    public async Task Incinerate_HasAThousandMillisecondGlobalCooldownHasteShortens()
    {
        var parser = await AnalyzeAsync([Combatant(), Cast(Spells.Incinerate.FSLID, 1000)]);

        var hastePercentage = StatTracker.RatingToPercentage(HasteRating);
        hastePercentage.ShouldBeGreaterThan(0);
        parser.Events.OfType<CastEvent>().Single().GlobalCooldown.ShouldNotBeNull().Duration
            .ShouldBe((int)Math.Round(GlobalCooldownMs / (1 + hastePercentage), MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task Wildfire_StartsNoGlobalCooldown()
    {
        var parser = await AnalyzeAsync([Combatant(), Cast(Spells.Wildfire.FSLID, 1000)]);

        parser.Events.OfType<CastEvent>().Single().GlobalCooldown.ShouldBeNull();
    }

    private static CombatantInfoEvent Combatant() => new() { SourceId = PlayerId, Haste = HasteRating };

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
