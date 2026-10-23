using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqAbilities = FellowshipAnalyzer.Heroes.Tariq.Modules.Abilities;
using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;
using TariqTalents = FellowshipAnalyzer.Core.Common.Spells.TariqTalents;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

/// <summary>
/// Ace of Spades (talent 675) gives Chain Lightning (ability 1237) 2 charges, and Haste then enters
/// Chain Lightning's cooldown as time rate.
/// </summary>
public sealed class AceOfSpadesTests
{
    private const int PlayerId = FuryEconomyAnalyzerTests.PlayerId;

    private static readonly int ChainLightningId = TariqSpells.ChainLightning.FSLID;

    [Fact]
    public void ChainLightning_CarriesTheGrantTheExportNames()
    {
        TariqTalents.AceOfSpades.ShouldBe(675);
        TariqSpells.ChainLightning.Id.ShouldBe(1237);
        TariqSpells.ChainLightning.ChargeGrant.ShouldBe(new ChargeGrant
        {
            Source = GrantSource.Talent,
            Id = TariqTalents.AceOfSpades,
            Charges = 2,
            CooldownHaste = CooldownHasteScaling.TimeRate,
        });
    }

    [Fact]
    public async Task WithAceOfSpades_ChainLightningHasTwoChargesAndTimeRateHaste()
    {
        var abilities = await AnalyzeAsync(talented: true);

        abilities.GetMaxCharges(ChainLightningId).ShouldBe(2);
        abilities.GetAbility(ChainLightningId).ShouldNotBeNull().CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);
    }

    [Fact]
    public async Task WithoutAceOfSpades_ChainLightningKeepsOneChargeAndDurationHaste()
    {
        var abilities = await AnalyzeAsync(talented: false);

        abilities.GetMaxCharges(ChainLightningId).ShouldBe(1);
        abilities.GetAbility(ChainLightningId).ShouldNotBeNull().CooldownHaste.ShouldBe(CooldownHasteScaling.Duration);
    }

    private static async Task<TariqAbilities> AnalyzeAsync(bool talented)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var dungeon = new ReportDungeon(0, "Boss", 1, true, 0, 60_000, null, null, null);
        await parser.Analyze(
            [
                new CombatantInfoEvent
                {
                    SourceId = PlayerId,
                    Talents = talented ? [new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, TariqTalents.AceOfSpades).Value }] : [],
                },
            ],
            playerId: PlayerId,
            dungeon: dungeon);
        return parser.GetModule<TariqAbilities>().ShouldNotBeNull();
    }
}
