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

    [Fact]
    public async Task RollingFlames_WithTalent_AccumulatesGeneratedFromTicks()
    {
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
        searingBlaze.CooldownReduction.Total.ShouldBe(750);
        searingBlaze.CooldownReduction.Effective.ShouldBe(0);
        searingBlaze.CooldownReduction.Wasted.ShouldBe(750);
    }

    [Fact]
    public async Task RollingFlames_ReducesEngulfingFlamesCooldown_FromBothSources()
    {
        var events = new List<Event>
        {
            CombatantWithRollingFlames(),
            Cast(Spells.EngulfingFlames.FSLID, 1000),
            SearingBlazeTick(2000),
            InfernalWaveCast(2100),
        };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldNotBeNull();
        analyzer.CooldownReductions.First(c => c.Spell.Id == Spells.SearingBlaze.Id).CooldownReduction.Effective.ShouldBe(250);
        analyzer.CooldownReductions.First(c => c.Spell.Id == Spells.InfernalWave.Id).CooldownReduction.Effective.ShouldBe(1000);
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
        await parser.Analyze(events, PlayerId, new ReportDungeon(0, "", 0, null, 0, 10000, null, null, null));
        return parser.GetModule<RollingFlamesAnalyzer>();
    }
}
