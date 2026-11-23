using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using ElarionAbilities = FellowshipAnalyzer.Heroes.Elarion.Modules.Abilities;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Analysis;

public sealed class ElarionSpellbookTests
{
    [Fact]
    public async Task EveryEnabledEntry_HasARealCategory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        await parser.Analyze([], playerId: 1, dungeon: new ReportDungeon(0, "", 0, null, 0, 0, null, null, null));

        var spellbook = parser.Abilities!.Spellbook().Where(e => e.Enabled).ToList();

        Assert.NotEmpty(spellbook);
        Assert.DoesNotContain(spellbook, e => e.Category == SpellCategory.Uncategorized);
    }

    [Fact]
    public void GlobalCooldown_ComesFromTheSpellRecord()
    {
        var spellbook = new ElarionAbilities().Spellbook().ToList();

        spellbook.ShouldAllBe(e => e.Gcd == null);
    }

    [Fact]
    public void Roll_TakesItsCooldownAndChargesFromTheSpellRecord()
    {
        var roll = new ElarionAbilities().Spellbook().Single(e => e.PrimarySpell.FSLID == Spells.Roll.FSLID);

        roll.PrimarySpell.ShouldBe(Spells.Roll);
        roll.PrimarySpell.Cooldown.ShouldBe(8);
        roll.PrimarySpell.Charges.ShouldBe(3);
    }
}
