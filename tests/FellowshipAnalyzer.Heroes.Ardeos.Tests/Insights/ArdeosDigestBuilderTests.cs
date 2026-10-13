using System.Text;
using System.Text.RegularExpressions;

using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Insights;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Insights;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Insights;

public sealed class ArdeosDigestBuilderTests
{
    private const int PlayerId = 7;
    private const int TargetId = 27;
    private const int TargetInstance = 2;
    private const int Anchor = 10_000;

    [Fact]
    public async Task CleanWildfireWindow_IsACompleteWindowWithAStrengthsFocus()
    {
        var parser = await AnalyzeAsync(CleanWindow());

        var message = ArdeosDigestBuilder.Wildfire(parser, Digest.Build(parser, []))!;

        message.Focus.ShouldBe(InsightFocus.Strengths);
        var window = message.Pulls.ShouldHaveSingleItem().Windows.ShouldHaveSingleItem();
        window.DotsMissing.ShouldBe([Spells.FireFrogs.Name, Spells.Incinerate.Name], ignoreOrder: true);
        window.EnoughDots.ShouldBeTrue();
        window.TwoEngulfingFlames.ShouldBeTrue();
        window.FollowUpComplete.ShouldBeTrue();
        window.CastsAfter.ShouldBe([Spells.Detonate.Name, Spells.Detonate.Name, Spells.Detonate.Name]);
    }

    [Fact]
    public async Task EveryDetonateCast_CarriesItsDotStack()
    {
        var parser = await AnalyzeAsync(CleanWindow());

        var message = ArdeosDigestBuilder.Detonate(parser, Digest.Build(parser, []))!;

        var casts = message.Pulls.ShouldHaveSingleItem().Casts;
        casts.Count.ShouldBe(3);
        casts[0].DotStack.ShouldBe("full");
        casts[0].Free.ShouldBeNull();
        message.Focus.ShouldBe(InsightFocus.Strengths);
    }

    [Fact]
    public async Task EngulfingFlamesSetup_WithBothChargesCast_IsFull()
    {
        var events = CleanWindow();
        events.Add(Cast(Spells.EngulfingFlames.FSLID, Anchor - 2000));
        events.Add(Cast(Spells.EngulfingFlames.FSLID, Anchor - 1900));
        events.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        var parser = await AnalyzeAsync(events);

        var message = ArdeosDigestBuilder.EngulfingFlames(parser, Digest.Build(parser, []))!;

        message.Pulls.ShouldHaveSingleItem().Setups.ShouldHaveSingleItem().Setup.ShouldBe("full");
    }

    [Fact]
    public async Task Messages_HoldNoNumberOrGrade()
    {
        var parser = await AnalyzeAsync(CleanWindow());

        var messages = ArdeosDigestBuilder.Messages(parser);

        messages.Keys.ShouldContain("wildfireCombo");
        foreach (var json in messages.Values.Select(Encoding.UTF8.GetString))
        {
            Regex.IsMatch(Regex.Unescape(json), @"\d").ShouldBeFalse(json);
            foreach (var grade in new[] { "Perfect", "Good", "\"Ok\"", "Bad", "grade" })
                json.ShouldNotContain(grade, Case.Insensitive);
        }
    }

    private static List<Event> CleanWindow() =>
    [
        new CombatantInfoEvent
        {
            SourceId = PlayerId,
            Gear = [new Item { Id = FellowshipAnalyzer.Core.Common.Items.Items.RingOfBoomtasticExplosions.Id }],
        },
        ApplyDebuff(Anchor - 3000, Spells.SearingBlazeDot.FSLID),
        ApplyDebuff(Anchor - 3000, Spells.FireBallDot.FSLID),
        ApplyDebuff(Anchor - 3000, Spells.ApocalypseDot.FSLID),
        ApplyDebuff(Anchor - 2000, Spells.EngulfingFlamesDot.FSLID),
        ApplyDebuff(Anchor - 1900, Spells.EngulfingFlamesDot.FSLID),
        ApplyBuff(Anchor, Spells.WildfireDotBonusBuff.FSLID),
        new CastEvent
        {
            Timestamp = Anchor,
            SourceId = PlayerId,
            TargetId = TargetId,
            TargetInstance = TargetInstance,
            Ability = new Ability { Id = Spells.Wildfire.FSLID },
        },
        Cast(Spells.Detonate.FSLID, Anchor + 500),
        Cast(Spells.Detonate.FSLID, Anchor + 1000),
        Cast(Spells.Detonate.FSLID, Anchor + 1500),
        RemoveBuff(Anchor + 9000, Spells.WildfireDotBonusBuff.FSLID),
    ];

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TargetId,
        TargetInstance = TargetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static async Task<ArdeosCombatLogParser> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        parser.Actors =
        [
            new(PlayerId, "Ardeos", "Player", null, null, null),
            new(TargetId, "Target", "NPC", null, null, null),
        ];
        await parser.Analyze(events, PlayerId, new ReportDungeon(0, "", 0, null, 0, 25_000, null, null, null));
        return parser;
    }
}
