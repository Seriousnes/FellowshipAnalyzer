using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

public sealed class UnfoldingDoomAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BossId = 40;
    private const int AddId = 41;
    private const int PullEnd = 60_000;
    private const int CooldownMs = 45_000;

    private static readonly ReportDungeon BossDungeon =
        new(Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
            StartTime: 0, EndTime: PullEnd, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static readonly ReportDungeon TrashDungeon =
        new(Id: 0, Name: "Trash", EncounterId: 0, Kill: true,
            StartTime: 0, EndTime: PullEnd, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task NoApplications_ReportsNoUptimeAndNoDamage()
    {
        var analyzer = await Measure(Damage(BossId, 10_000, 1_200));

        analyzer.Casts.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(0);
        analyzer.Uptime.ShouldBe(0d);
        analyzer.TargetUptimes.ShouldBeEmpty();
        analyzer.DamageWhileActive.ShouldBe(0);
        analyzer.DamageGained.ShouldBe(0);
        analyzer.Applications.ShouldAllBe(application => application.Damage == 0);
        analyzer.Reapplications.ShouldBeEmpty();
        analyzer.OverlappedMs.ShouldBe(0);
    }

    [Fact]
    public async Task WindowsOnTwoEnemies_UptimeCountsOverlappingTimeOnce()
    {
        var analyzer = await Measure(
            Apply(BossId, 1_000),
            Apply(AddId, 6_000),
            Remove(BossId, 11_000),
            Remove(AddId, 16_000));

        analyzer.TargetUptimes.Count.ShouldBe(2);
        analyzer.ActiveMs.ShouldBe(15_000);
        analyzer.Uptime.ShouldBe(15_000 / (double)PullEnd, 0.0001);
    }

    [Fact]
    public async Task PlayerDamageInsideAWindow_CountsTowardsTheDamageGained()
    {
        var analyzer = await Measure(
            Apply(BossId, 1_000),
            Damage(BossId, 5_000, 1_200),
            Remove(BossId, 11_000),
            Damage(BossId, 15_000, 1_200));

        analyzer.DamageWhileActive.ShouldBe(1_200);
        analyzer.DamageGained.ShouldBe(200);

        var application = analyzer.Applications.ShouldHaveSingleItem();
        application.Damage.ShouldBe(1_200);
        application.DamageGained.ShouldBe(200);
        application.ActiveMs.ShouldBe(10_000);
        application.Start.ShouldBe(1_000);
        application.End.ShouldBe(11_000);
    }

    [Fact]
    public async Task DamageAbsorbedInsideAWindow_CountsTowardsTheDamageGained()
    {
        var damage = Damage(BossId, 5_000, 200);
        damage.Absorbed = 1_000;

        var analyzer = await Measure(Apply(BossId, 1_000), damage, Remove(BossId, 11_000));

        analyzer.DamageWhileActive.ShouldBe(1_200);
        analyzer.DamageGained.ShouldBe(200);
    }

    [Fact]
    public async Task DamageOnTwoDebuffedEnemies_IsSplitAcrossTheirApplications()
    {
        var analyzer = await Measure(
            Apply(BossId, 1_000),
            Apply(AddId, 1_000),
            Damage(AddId, 3_000, 600),
            Damage(BossId, 5_000, 1_200),
            Remove(BossId, 11_000),
            Remove(AddId, 11_000));

        analyzer.DamageWhileActive.ShouldBe(1_800);
        analyzer.Applications.Count.ShouldBe(2);

        var boss = analyzer.Applications.Single(application => application.Unit.ActorId == BossId);
        boss.Damage.ShouldBe(1_200);
        boss.DamageGained.ShouldBe(200);

        var add = analyzer.Applications.Single(application => application.Unit.ActorId == AddId);
        add.Damage.ShouldBe(600);
        add.DamageGained.ShouldBe(100);
    }

    [Fact]
    public async Task DamageToOneSpawnOfAnEnemy_IsNotAttributedToAnother()
    {
        var apply = Apply(BossId, 1_000);
        apply.TargetInstance = 1;
        var remove = Remove(BossId, 11_000);
        remove.TargetInstance = 1;
        var otherSpawn = Damage(BossId, 5_000, 1_200);
        otherSpawn.TargetInstance = 2;

        var analyzer = await Measure(apply, otherSpawn, remove);

        analyzer.DamageWhileActive.ShouldBe(0);
        analyzer.TargetUptimes.ShouldHaveSingleItem().Unit.Instance.ShouldBe(1);
    }

    [Fact]
    public async Task DamageToAnEnemyWithNoWindow_IsNotCounted()
    {
        var analyzer = await Measure(
            Apply(BossId, 1_000),
            Damage(AddId, 5_000, 1_200),
            Remove(BossId, 11_000));

        analyzer.DamageWhileActive.ShouldBe(0);
        analyzer.Applications.ShouldAllBe(application => application.Damage == 0);
    }

    [Fact]
    public async Task DamageByAnotherPlayer_IsNotCounted()
    {
        var damage = Damage(BossId, 5_000, 1_200);
        damage.SourceId = PlayerId + 1;

        var analyzer = await Measure(Apply(BossId, 1_000), damage, Remove(BossId, 11_000));

        analyzer.DamageWhileActive.ShouldBe(0);
    }

    [Fact]
    public async Task ReapplyingWhileTheDebuffIsActive_RecordsTheDiscardedDuration()
    {
        var analyzer = await Measure(Apply(BossId, 1_000), Apply(BossId, 6_000), Remove(BossId, 20_000));

        var reapplication = analyzer.Reapplications.ShouldHaveSingleItem();
        reapplication.Timestamp.ShouldBe(6_000);
        reapplication.OverlappedMs.ShouldBe(15_000);
        analyzer.OverlappedMs.ShouldBe(15_000);
        analyzer.ActiveMs.ShouldBe(19_000);
    }

    [Fact]
    public async Task RefreshingAnOpenWindow_RecordsTheDiscardedDuration()
    {
        var analyzer = await Measure(Apply(BossId, 1_000), Refresh(BossId, 5_000), Remove(BossId, 25_000));

        analyzer.Reapplications.ShouldHaveSingleItem().OverlappedMs.ShouldBe(16_000);
    }

    [Fact]
    public async Task ReapplyingAfterTheFullDurationElapsed_DiscardsNothing()
    {
        var analyzer = await Measure(Apply(BossId, 1_000), Apply(BossId, 25_000), Remove(BossId, 30_000));

        analyzer.Reapplications.ShouldHaveSingleItem().OverlappedMs.ShouldBe(0);
        analyzer.OverlappedMs.ShouldBe(0);
    }

    [Fact]
    public async Task ReapplyingAfterARemoval_IsNotAReapplication()
    {
        var analyzer = await Measure(
            Apply(BossId, 1_000),
            Remove(BossId, 11_000),
            Apply(BossId, 16_000),
            Remove(BossId, 26_000));

        analyzer.Reapplications.ShouldBeEmpty();
        analyzer.OverlappedMs.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task NeverCast_LeavesTheAbilityAvailableForTheWholePull()
    {
        var analyzer = await Measure(Apply(BossId, 10_000), Remove(BossId, 20_000));

        analyzer.Casts.ShouldBe(0);
        analyzer.AvailableMs.ShouldBe(PullEnd);
        analyzer.IdleAvailableMs.ShouldBe(PullEnd - 10_000);
        analyzer.IdleAvailableShare.ShouldBe((PullEnd - 10_000) / (double)PullEnd, 0.0001);
    }

    [Fact]
    public async Task DelayAfterReady_IsTheIdleStretchTheApplicationClosed()
    {
        var analyzer = await Measure(Apply(BossId, 3_000), Remove(BossId, 23_000));

        analyzer.Applications.ShouldHaveSingleItem().DelayAfterReadyMs.ShouldBe(3_000);
    }

    [Fact]
    public async Task DelayAfterReady_IsZeroForAnApplicationAtThePullStart()
    {
        var analyzer = await Measure(Apply(BossId, 0), Remove(BossId, 20_000));

        analyzer.Applications.ShouldHaveSingleItem().DelayAfterReadyMs.ShouldBe(0);
    }

    [Fact]
    public async Task DelayAfterReady_IsZeroForAnApplicationOntoASecondEnemyWhileTheFirstIsDebuffed()
    {
        var analyzer = await Measure(
            Apply(BossId, 0),
            Apply(AddId, 5_000),
            Remove(AddId, 15_000),
            Remove(BossId, 20_000));

        var add = analyzer.Applications.Single(application => application.Unit.ActorId == AddId);
        add.DelayAfterReadyMs.ShouldBe(0);
    }

    [Fact]
    public async Task ACast_ClosesTheAvailableWindowUntilTheCooldownEnds()
    {
        var analyzer = await Measure(
            Cast(5_000),
            Apply(BossId, 5_000),
            Remove(BossId, 25_000));

        analyzer.Casts.ShouldBe(1);
        analyzer.AvailableMs.ShouldBe(5_000 + (PullEnd - (5_000 + CooldownMs)));
        analyzer.IdleAvailableMs.ShouldBe(analyzer.AvailableMs);
    }

    [Fact]
    public async Task ACooldownRunningAtThePullStart_ContinuesIntoTheNextPull()
    {
        var dungeon = BossDungeon with
        {
            EndTime = 120_000,
            DungeonPulls =
            [
                new DungeonPull(1, 1, true, 0, 30_000, "First", null),
                new DungeonPull(2, 1, true, 40_000, 120_000, "Second", null),
            ],
        };

        var parser = await AnalyzeAsync(dungeon, Cast(20_000), Apply(BossId, 20_000), Remove(BossId, 40_000));

        var analyzers = Analyzers(parser);
        analyzers.Count.ShouldBe(2);

        analyzers[0].Casts.ShouldBe(1);
        analyzers[0].AvailableMs.ShouldBe(20_000);

        analyzers[1].Casts.ShouldBe(0);
        analyzers[1].AvailableMs.ShouldBe(120_000 - (20_000 + CooldownMs));
        analyzers[1].IdleAvailableMs.ShouldBe(analyzers[1].AvailableMs);
    }

    [Fact]
    public async Task AWindowStillOpenWhileTheAbilityIsAvailable_IsNotIdleTime()
    {
        var analyzer = await Measure(Apply(BossId, 0), Remove(BossId, 20_000));

        analyzer.AvailableMs.ShouldBe(PullEnd);
        analyzer.IdleAvailableMs.ShouldBe(PullEnd - 20_000);
    }

    [Fact]
    public async Task AnApplicationWithNoRemoval_DoesNotRunToThePullEnd()
    {
        var analyzer = await Measure(Apply(BossId, 10_000));

        analyzer.ActiveMs.ShouldBe(0);
        analyzer.TargetUptimes.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithHasteningDoom_TheTalentIsTaken()
    {
        var analyzer = await Measure(Talented(AeonaTalents.HasteningDoom), Apply(BossId, 1_000), Remove(BossId, 11_000));

        analyzer.HasteningDoomTaken.ShouldBeTrue();
    }

    [Fact]
    public async Task WithoutHasteningDoom_TheAnalyzerStillRuns()
    {
        var analyzer = await Measure(Apply(BossId, 1_000), Remove(BossId, 11_000));

        analyzer.HasteningDoomTaken.ShouldBeFalse();
        analyzer.ActiveMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task OnATrashPull_TheAnalyzerRuns()
    {
        var parser = await AnalyzeAsync(TrashDungeon, Apply(BossId, 1_000), Remove(BossId, 11_000));

        var entry = parser.UnfoldingDoomAnalyzers.ShouldHaveSingleItem();
        entry.Pull.Targets.ShouldBe(PullKind.Multi);
        entry.Analyzer.ShouldBeOfType<UnfoldingDoomAnalyzer>().ActiveMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task TheAnalyzerIsReachableFromEveryGeneratedReadPath()
    {
        var parser = await AnalyzeAsync(BossDungeon, Apply(BossId, 1_000), Remove(BossId, 11_000));

        var entry = parser.UnfoldingDoomAnalyzers.ShouldHaveSingleItem();
        entry.Pull.UnfoldingDoomAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).UnfoldingDoomAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static ApplyDebuffEvent Apply(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.UnfoldingDoomDebuff.FSLID },
    };

    private static RefreshDebuffEvent Refresh(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.UnfoldingDoomDebuff.FSLID },
    };

    private static RemoveDebuffEvent Remove(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.UnfoldingDoomDebuff.FSLID },
    };

    private static DamageEvent Damage(int targetId, int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = amount,
        Ability = new Ability { Id = Spells.TimeShardDamage.FSLID },
    };

    private static CastEvent Cast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = Spells.UnfoldingDoom.FSLID },
    };

    private static CombatantInfoEvent Talented(int talentId) => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = talentId }],
    };

    private static List<UnfoldingDoomAnalyzer> Analyzers(AeonaCombatLogParser parser) =>
        [.. parser.UnfoldingDoomAnalyzers.Select(entry => entry.Analyzer).OfType<UnfoldingDoomAnalyzer>()];

    private static async Task<UnfoldingDoomAnalyzer> Measure(params Event[] events)
    {
        var parser = await AnalyzeAsync(BossDungeon, events);
        return parser.UnfoldingDoomAnalyzers
            .ShouldHaveSingleItem()
            .Analyzer
            .ShouldBeOfType<UnfoldingDoomAnalyzer>();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeAsync(ReportDungeon dungeon, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, dungeon);
        return parser;
    }
}
