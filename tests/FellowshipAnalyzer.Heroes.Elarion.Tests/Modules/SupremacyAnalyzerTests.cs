using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class SupremacyAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 40_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task FourMultishotsInsideAFourSecondWindow_AreAllCounted()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            Multishot(1_300),
            Multishot(2_200),
            Multishot(3_100),
            Multishot(4_000),
            RemoveBuff(5_000));

        analyzer.EmpoweredMultishotCasts.ShouldBe(4);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StartMs.ShouldBe(1_000);
        window.EndMs.ShouldBe(5_000);
        window.MultishotCasts.ShouldBe(4);
        window.FirstMultishotDelayMs.ShouldBe(300);
    }

    [Fact]
    public async Task StackEventsOnAFerventWindow_DoNotChangeTheCount()
    {
        var (parser, _) = await AnalyzeAsync(
            FerventSupremacyBuild(),
            ApplyBuff(1_000),
            ApplyBuffStack(1_000, stack: 4),
            Multishot(1_300),
            RemoveBuffStack(1_302, stack: 3),
            Multishot(2_200),
            RemoveBuffStack(2_202, stack: 2),
            Multishot(3_100),
            RemoveBuffStack(3_102, stack: 1),
            Multishot(4_000),
            RemoveBuff(4_002));

        var analyzer = parser.SupremacyAnalyzers.ShouldHaveSingleItem().Analyzer;
        parser.SelectedCombatant.HasTalent(Talents.FerventSupremacy.Id).ShouldBeTrue();
        analyzer.Windows.ShouldHaveSingleItem().MultishotCasts.ShouldBe(4);
    }

    [Fact]
    public async Task MultishotOutsideAnyWindow_IsNotCounted()
    {
        var analyzer = await Analyze(
            Multishot(1_000),
            ApplyBuff(2_000),
            Multishot(2_500),
            RemoveBuff(6_000),
            Multishot(7_000));

        analyzer.EmpoweredMultishotCasts.ShouldBe(1);
        analyzer.Windows.ShouldHaveSingleItem().MultishotCasts.ShouldBe(1);
    }

    [Fact]
    public async Task WindowStillOpenAtPullEnd_ClosesAtThePullEnd()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            Multishot(1_500),
            Multishot(2_400));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.EndMs.ShouldBe(analyzer.Pull.EndTime);
        window.MultishotCasts.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveBuffWithNoOpenWindow_IsIgnored()
    {
        var analyzer = await Analyze(
            RemoveBuff(1_000),
            Multishot(2_000));

        analyzer.Windows.ShouldBeEmpty();
        analyzer.EmpoweredMultishotCasts.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyBuffWhileAWindowIsOpen_ClosesThePreviousWindow()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            Multishot(1_500),
            ApplyBuff(9_000),
            Multishot(9_400),
            Multishot(10_300),
            RemoveBuff(13_000));

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].EndMs.ShouldBe(9_000);
        analyzer.Windows[0].MultishotCasts.ShouldBe(1);
        analyzer.Windows[1].MultishotCasts.ShouldBe(2);
        analyzer.Windows[1].FirstMultishotDelayMs.ShouldBe(400);
        analyzer.EmpoweredMultishotCasts.ShouldBe(3);
    }

    [Fact]
    public async Task WindowWithNoMultishot_HasNoOpeningDelay()
    {
        var analyzer = await Analyze(
            ApplyBuff(1_000),
            RemoveBuff(5_000));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.MultishotCasts.ShouldBe(0);
        window.FirstMultishotDelayMs.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_SupremacyWindows_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            ApplyBuff(1_000),
            Multishot(1_400),
            RemoveBuff(5_000));

        var entry = parser.SupremacyAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.SupremacyAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).SupremacyAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CombatantInfoEvent FerventSupremacyBuild() => new()
    {
        Timestamp = 0,
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = Talents.FerventSupremacy.FSLID }],
    };

    private static CastEvent Multishot(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Activation = true,
        Ability = new SpellAbility { FSLID = Spells.Multishot.FSLID, Name = Spells.Multishot.Name },
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = BuffAbility(),
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static RemoveBuffStackEvent RemoveBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = BuffAbility(),
    };

    private static SpellAbility BuffAbility() => new()
    {
        FSLID = Spells.SkystridersSupremacyBuff.FSLID,
        Name = Spells.SkystridersSupremacyBuff.Name,
    };

    private static async Task<SupremacyAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(events);
        return parser.SupremacyAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(ElarionCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        var result = await parser.Analyze([.. events], PlayerId, Dungeon);
        return (parser, result);
    }
}
