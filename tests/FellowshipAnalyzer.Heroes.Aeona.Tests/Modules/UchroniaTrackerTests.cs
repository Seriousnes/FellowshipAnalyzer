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

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class UchroniaTrackerTests
{
    private const int PlayerId = 7;
    private const int DungeonEndTime = 30_000;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
            StartTime: 0, EndTime: DungeonEndTime, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task WithoutTheTalent_TheModuleIsInactive()
    {
        var parser = await AnalyzeAsync(Apply(1_000), Remove(5_000));

        parser.UchroniaTracker.ShouldBeNull();
        parser.GetModule<UchroniaTracker>().ShouldBeNull();
    }

    [Fact]
    public async Task ApplyThenRemove_OpensAndClosesOneWindow()
    {
        var tracker = await Track(Apply(1_000), Remove(5_000));

        var window = tracker.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1_000);
        window.End.ShouldBe(5_000);
        tracker.Procs.ShouldBe(1);
        tracker.IsActive(3_000).ShouldBeTrue();
        tracker.IsActive(1_000).ShouldBeTrue();
        tracker.IsActive(5_000).ShouldBeTrue();
        tracker.IsActive(5_001).ShouldBeFalse();
        tracker.IsActive(999).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyInsideAnOpenWindow_DoesNotOpenASecond()
    {
        var tracker = await Track(Apply(1_000), Apply(2_000), Refresh(3_000), Remove(5_000));

        var window = tracker.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1_000);
        window.End.ShouldBe(5_000);
        tracker.Procs.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshWithoutApply_OpensAWindow()
    {
        var tracker = await Track(Refresh(2_000), Remove(6_000));

        var window = tracker.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(2_000);
        window.End.ShouldBe(6_000);
    }

    [Fact]
    public async Task WindowStillOpenAtTheDungeonEnd_ClosesAtTheDungeonEnd()
    {
        var tracker = await Track(Apply(1_000), Remove(5_000), Apply(9_000));

        tracker.Procs.ShouldBe(2);
        tracker.Windows.Count.ShouldBe(2);
        tracker.Windows[1].Start.ShouldBe(9_000);
        tracker.Windows[1].End.ShouldBe(DungeonEndTime);
        tracker.IsActive(20_000).ShouldBeTrue();
        tracker.IsActive(DungeonEndTime).ShouldBeTrue();
        tracker.IsActive(DungeonEndTime + 1).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveWithNoOpenWindow_OpensNothing()
    {
        var tracker = await Track(Remove(5_000));

        tracker.Windows.ShouldBeEmpty();
        tracker.Procs.ShouldBe(0);
    }

    private static ApplyBuffEvent Apply(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.Uchronia.FSLID },
    };

    private static RefreshBuffEvent Refresh(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.Uchronia.FSLID },
    };

    private static RemoveBuffEvent Remove(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.Uchronia.FSLID },
    };

    private static CombatantInfoEvent Talented() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = AeonaTalents.Uchronia }],
    };

    private static async Task<UchroniaTracker> Track(params Event[] events)
    {
        var parser = await AnalyzeAsync([Talented(), .. events]);
        return parser.UchroniaTracker.ShouldNotBeNull();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeAsync(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Dungeon);
        return parser;
    }
}
