using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class RollingFlamesAnalyzerTests
{
    private const int PlayerId = 7;
    private const int RollingFlamesTalentId = 226;

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(250, 100, 150, 40)]
    [InlineData(250, 250, 0, 100)]
    [InlineData(250, 0, 250, 0)]
    [InlineData(1000, 750, 250, 75)]
    public void RollingFlamesCdr_DerivesWastedAndEfficiency(int generated, int effective, int wasted, int efficiencyPct)
    {
        var cdr = new RollingFlamesCdr(Spells.SearingBlaze, generated, effective);

        cdr.WastedMs.ShouldBe(wasted);
        ((int)Math.Round(cdr.Efficiency * 100)).ShouldBe(efficiencyPct);
    }

    [Fact]
    public async Task RollingFlames_WithTalent_AccumulatesGeneratedFromTicks()
    {
        // Searing Blaze damage generates Engulfing Flames CDR; with EF never cast (never on cooldown)
        // all of it is wasted.
        var events = new List<Event>
        {
            CombatantWithRollingFlames(),
            SearingBlazeTick(1000),
            SearingBlazeTick(2000),
            SearingBlazeTick(3000),
        };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldNotBeNull();
        var searingBlaze = analyzer.CooldownReductions.First(c => c.Spell.Id == Spells.SearingBlaze.Id);
        searingBlaze.GeneratedMs.ShouldBe(750);
        searingBlaze.EffectiveMs.ShouldBe(0);
        searingBlaze.WastedMs.ShouldBe(750);
    }

    [Fact]
    public async Task RollingFlames_ReducesEngulfingFlamesCooldown_FromBothSources()
    {
        // Cast EF to put it on cooldown, then drive CDR from Searing Blaze damage and Infernal Wave
        // casts. Both must shorten Engulfing Flames' running cooldown.
        var events = new List<Event>
        {
            CombatantWithRollingFlames(),
            Cast(Spells.EngulfingFlames.FSLID, 1000),
            SearingBlazeTick(2000),
            InfernalWaveCast(2100),
        };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldNotBeNull();
        analyzer.CooldownReductions.First(c => c.Spell.Id == Spells.SearingBlaze.Id).EffectiveMs.ShouldBe(250);
        analyzer.CooldownReductions.First(c => c.Spell.Id == Spells.InfernalWave.Id).EffectiveMs.ShouldBe(1000);
    }

    [Fact]
    public async Task RollingFlames_WithoutTalent_IsInactive()
    {
        var events = new List<Event> { SearingBlazeTick(1000) };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldBeNull();
    }

    private static CombatantInfoEvent CombatantWithRollingFlames() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = RollingFlamesTalentId }],
    };

    private static DamageEvent SearingBlazeTick(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static CastEvent InfernalWaveCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = Spells.InfernalWave.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static async Task<RollingFlamesAnalyzer?> AnalyzeAndGetAnalyzer(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        await parser.Analyze(events, PlayerId, new ReportFight(0, "", 0, null, 0, 10000, null, null, null));
        return parser.GetModule<RollingFlamesAnalyzer>();
    }
}
