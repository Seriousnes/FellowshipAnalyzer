using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Mara.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using MaraAbilities = FellowshipAnalyzer.Heroes.Mara.Modules.Abilities;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaraSpellbookTests
{
    private const int PlayerId = 7;
    private const int HasteRating = 1000;
    private const int GlobalCooldownMs = 1000;

    [Fact]
    public void EveryEntry_HasARealCategory()
    {
        var spellbook = new MaraAbilities().Spellbook().ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public void NoEntry_DeclaresAGlobalCooldown()
    {
        foreach (var entry in new MaraAbilities().Spellbook())
            entry.Gcd.ShouldBeNull();
    }

    [Fact]
    public void Backstab_HasAOneSecondGlobalCooldownHasteDoesNotShorten()
    {
        Spells.Backstab.GlobalCooldown.ShouldBe(1);
        Spells.Backstab.GlobalCooldownScalesWithHaste.ShouldBe(false);
    }

    [Fact]
    public void MaidenOfDeathAndMatriarchMacabre_AreOffTheGlobalCooldown()
    {
        var spellbook = new MaraAbilities().Spellbook().ToList();

        Entry(spellbook, Spells.MaidenOfDeath).OnGlobalCooldown.ShouldBeFalse();
        Entry(spellbook, Spells.MatriarchMacabre).OnGlobalCooldown.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1129)]
    [InlineData(2308)]
    public async Task BackstabCasts_HaveAThousandMillisecondGlobalCooldownAtHaste(int abilityId)
    {
        var parser = await AnalyzeAsync([Combatant(), Cast(abilityId, 1000)]);

        StatTracker.RatingToPercentage(HasteRating).ShouldBeGreaterThan(0);
        parser.Events.OfType<CastEvent>().Single().GlobalCooldown.ShouldNotBeNull().Duration.ShouldBe(GlobalCooldownMs);
    }

    [Fact]
    public async Task MaidenOfDeath_StartsNoGlobalCooldown()
    {
        var parser = await AnalyzeAsync([Combatant(), Cast(Spells.MaidenOfDeath.Id, 1000)]);

        parser.Events.OfType<CastEvent>().Single().GlobalCooldown.ShouldBeNull();
    }

    private static SpellbookAbility Entry(List<SpellbookAbility> spellbook, Spell spell) =>
        spellbook.Single(entry => entry.PrimarySpell.FSLID == spell.FSLID);

    private static CombatantInfoEvent Combatant() => new() { SourceId = PlayerId, Haste = HasteRating };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static async Task<MaraCombatLogParser> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        await parser.Analyze(events, PlayerId, new ReportDungeon(0, "", 1, null, 0, 20000, null, null, null));
        return parser;
    }
}
