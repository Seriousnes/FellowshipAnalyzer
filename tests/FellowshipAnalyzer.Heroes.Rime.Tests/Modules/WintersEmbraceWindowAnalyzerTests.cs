using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Modules;

public sealed class WintersEmbraceWindowAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;
    private const int EnemyInstance = 1;
    private const int OtherEnemyId = 200;

    private const int WindowStart = 1_000;
    private const int WindowEnd = 4_000;

    private static readonly ReportFight BossFight = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 60_000, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null);

    private static readonly IReadOnlyList<DungeonPull> TrashPulls =
    [
        new(Id: 1, EncounterId: 0, Kill: null, StartTime: 0, EndTime: 20_000, Name: "Trash", EnemyNpcs: null),
    ];

    private static readonly ReportFight TrashFight = new(
        Id: 0, Name: "Dungeon", EncounterId: 0, Kill: null,
        StartTime: 0, EndTime: 20_000, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null, InProgress: false,
        DungeonPulls: TrashPulls);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(5, false)]
    public async Task OrbSnapshotOnOpeningCast_IsCapturedAndStarvedAtOrBelowOneOrb(int orbs, bool starved)
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OrbsBankedAtOpen.ShouldBe(orbs);
        window.OpenedStarved.ShouldBe(starved);
        analyzer.WindowsWithOrbData.ShouldBe(1);
        analyzer.StarvedWindows.ShouldBe(starved ? 1 : 0);
    }

    [Fact]
    public async Task OpeningCastWithoutResourceSnapshot_LeavesOrbsUnknownAndNotStarved()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs: null),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OrbsBankedAtOpen.ShouldBeNull();
        window.OpenedStarved.ShouldBeFalse();
        analyzer.WindowsWithOrbData.ShouldBe(0);
        analyzer.StarvedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task BurstingIceTargetDyingInsideWindow_EndsTheWindowByTargetDeath()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            Death(WindowStart + 500, EnemyId, EnemyInstance),
            EmbraceRemoved(WindowEnd));

        analyzer.Windows.ShouldHaveSingleItem().EndedByTargetDeath.ShouldBeTrue();
        analyzer.WindowsEndedByTargetDeath.ShouldBe(1);
    }

    [Fact]
    public async Task UnrelatedEnemyDyingInsideWindow_DoesNotEndTheWindowByTargetDeath()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            Death(WindowStart + 500, OtherEnemyId, targetInstance: 3),
            EmbraceRemoved(WindowEnd));

        analyzer.Windows.ShouldHaveSingleItem().EndedByTargetDeath.ShouldBeFalse();
        analyzer.WindowsEndedByTargetDeath.ShouldBe(0);
    }

    [Fact]
    public async Task MajorStandingWhenWindowOpens_IsSampledOntoTheWindow()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BuffApplied(500, Spells.IceBlitzBuff),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.MajorActiveAtOpen.ShouldBeTrue();
        window.MajorsActiveAtOpen.ShouldHaveSingleItem().ShouldBe(Spells.IceBlitzBuff.Name);
    }

    [Fact]
    public async Task NoMajorStandingWhenWindowOpens_LeavesTheWindowUnstacked()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.MajorActiveAtOpen.ShouldBeFalse();
        window.MajorsActiveAtOpen.ShouldBeEmpty();
    }

    [Fact]
    public async Task SingleTargetWindowOpenedOnTheSpender_ScoresSuccessful()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem()
            .ShouldBeOfType<SingleTargetEmbraceWindowAnalyzer.SingleTargetWindowEvaluation>();
        window.ExpectedStSpenderCount.ShouldBe(1);
        window.ExpectsFinisher.ShouldBeFalse();
        window.SpenderCount.ShouldBe(1);
        window.Successful.ShouldBeTrue();
        analyzer.SuccessfulWindows.ShouldBe(1);
    }

    [Fact]
    public async Task SingleTargetWindowWithNoSpender_ScoresFailing()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            BurstingIce(WindowStart, orbs: 0),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.FrostBolt),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.SpenderCount.ShouldBe(0);
        window.Successful.ShouldBeFalse();
        window.Partial.ShouldBeFalse();
    }

    [Fact]
    public async Task AoeWindowWithTwoSpendersAndColdSnap_ScoresSuccessful()
    {
        var analyzer = await AnalyzeMulti(
            Combatant(),
            BurstingIce(WindowStart, orbs: 5),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.IceComet),
            Cast(WindowStart + 1_600, Spells.IceComet),
            Cast(WindowStart + 2_600, Spells.ColdSnap),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem()
            .ShouldBeOfType<AoeEmbraceWindowAnalyzer.AoeWindowEvaluation>();
        window.AoeSpenderCount.ShouldBe(2);
        window.ColdSnapCount.ShouldBe(1);
        window.HasRequiredAoeSpenders.ShouldBeTrue();
        window.HasRequiredColdSnap.ShouldBeTrue();
        window.Successful.ShouldBeTrue();
    }

    [Fact]
    public async Task AoeWindowWithOneSpender_ScoresPartial()
    {
        var analyzer = await AnalyzeMulti(
            Combatant(),
            BurstingIce(WindowStart, orbs: 3),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.IceComet),
            Cast(WindowStart + 1_600, Spells.ColdSnap),
            EmbraceRemoved(WindowEnd));

        var window = analyzer.Windows.ShouldHaveSingleItem()
            .ShouldBeOfType<AoeEmbraceWindowAnalyzer.AoeWindowEvaluation>();
        window.AoeSpenderCount.ShouldBe(1);
        window.HasRequiredAoeSpenders.ShouldBeFalse();
        window.Successful.ShouldBeFalse();
        window.Partial.ShouldBeTrue();
    }

    [Fact]
    public async Task IcyTalonsTalent_SwitchesTheBuildAndCountsTalonStrikeAsTheSpender()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(RimeTalents.IcyTalons),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.TalonStrike),
            EmbraceRemoved(WindowEnd));

        analyzer.Build.ShouldBe(RimeBuild.IcyTalons);
        analyzer.StSpenderName.ShouldBe(Spells.TalonStrike.Name);
        analyzer.AoeSpenderName.ShouldBe(Spells.RisingTalons.Name);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StSpenderCount.ShouldBe(1);
        window.SpenderCount.ShouldBe(1);
        window.Successful.ShouldBeTrue();
    }

    [Fact]
    public async Task CombatantWithTalentsButWithoutIcyTalons_StaysOnTheDefaultBuild()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(RimeTalents.Avalanche),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.TalonStrike),
            EmbraceRemoved(WindowEnd));

        analyzer.Build.ShouldBe(RimeBuild.Default);
        analyzer.Windows.ShouldHaveSingleItem().StSpenderCount.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveWithoutALiveWindow_IsIgnored()
    {
        var analyzer = await AnalyzeSingleTarget(
            Combatant(),
            EmbraceRemoved(500),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        analyzer.EvaluatedWindows.ShouldBe(1);
        analyzer.Windows[0].StartTimestamp.ShouldBe(WindowStart);
    }

    [Fact]
    public async Task WindowStillOpenAtPullEnd_ClosesAtTheBaselineExpiryAndIsMarkedTruncated()
    {
        var analyzer = await AnalyzeMulti(
            Combatant(),
            BurstingIce(19_000, orbs: 4),
            EmbraceApplied(19_000),
            Cast(19_100, Spells.IceComet));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.BoundaryTruncated.ShouldBeTrue();
        window.EndTimestamp.ShouldBe(20_000);
        window.DurationMs.ShouldBe(1_000);
    }

    [Fact]
    public async Task BurstingIceDamage_CountsUniqueEnemySpawnsSeparately()
    {
        var analyzer = await AnalyzeMulti(
            Combatant(),
            BurstingIce(WindowStart, orbs: 5),
            EmbraceApplied(WindowStart),
            BurstingIceDamage(WindowStart + 200, EnemyId, 1),
            BurstingIceDamage(WindowStart + 200, EnemyId, 2),
            BurstingIceDamage(WindowStart + 400, EnemyId, 2),
            EmbraceRemoved(WindowEnd));

        analyzer.Windows.ShouldHaveSingleItem().TargetCount.ShouldBe(2);
    }

    [Fact]
    public async Task PerPullReadPathsResolveToTheSameAnalyzerInstance()
    {
        var parser = await Analyze(
            BossFight,
            Combatant(),
            BurstingIce(WindowStart, orbs: 4),
            EmbraceApplied(WindowStart),
            Cast(WindowStart + 100, Spells.GlacialBlast),
            EmbraceRemoved(WindowEnd));

        var entry = parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem();
        entry.Analyzer.ShouldBeOfType<SingleTargetEmbraceWindowAnalyzer>();
        entry.Pull.WintersEmbraceWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).WintersEmbraceWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CombatantInfoEvent Combatant(params int[] nativeTalentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. nativeTalentIds.Select(id => new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, id) })],
    };

    private static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = EnemyInstance,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    /// <summary>
    /// A Bursting Ice cast, optionally carrying the Winter Orb snapshot the log would attach. Raw
    /// Fellowship resource values are scaled by 100, so the in-game orb count is stored multiplied.
    /// </summary>
    private static CastEvent BurstingIce(int timestamp, int? orbs)
    {
        var cast = Cast(timestamp, Spells.BurstingIce);
        if (orbs is not null)
        {
            cast.SourceResources = new ActorResources
            {
                Resources = [new ClassResource { Type = ResourceTypes.Tertiary, Amount = orbs.Value * 100, Max = 500 }],
            };
        }

        return cast;
    }

    private static ApplyBuffEvent EmbraceApplied(int timestamp) => BuffApplied(timestamp, Spells.WintersEmbrace);

    private static ApplyBuffEvent BuffApplied(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static RemoveBuffEvent EmbraceRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Spells.WintersEmbrace.FSLID, Name = Spells.WintersEmbrace.Name },
    };

    private static DamageEvent BurstingIceDamage(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = Spells.BurstingIceDamage.FSLID, Name = Spells.BurstingIceDamage.Name },
    };

    private static DeathEvent Death(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        TargetId = targetId,
        TargetInstance = targetInstance,
    };

    private static async Task<WintersEmbraceWindowAnalyzer> AnalyzeSingleTarget(params Event[] events)
    {
        var parser = await Analyze(BossFight, events);
        return parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<WintersEmbraceWindowAnalyzer> AnalyzeMulti(params Event[] events)
    {
        var parser = await Analyze(TrashFight, events);
        return parser.WintersEmbraceWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<RimeCombatLogParser> Analyze(ReportFight fight, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, fight);
        return parser;
    }
}
