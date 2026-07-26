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

public sealed class SupremacyWindowAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 40_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task FourMultishots_DrainTheWindowCompletely()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 3),
            Multishot(3_000),
            RemoveBuffStack(3_002, stack: 2),
            Multishot(4_000),
            RemoveBuffStack(4_002, stack: 1),
            Multishot(5_000),
            RemoveBuff(5_001));

        analyzer.SupremacyCasts.ShouldBe(1);
        analyzer.EmpoweredMultishotCasts.ShouldBe(4);
        analyzer.RegularMultishotCasts.ShouldBe(0);
        analyzer.StacksGranted.ShouldBe(4);
        analyzer.StacksConsumed.ShouldBe(4);
        analyzer.StacksWasted.ShouldBe(0);
        analyzer.StacksConsumedPercentage.ShouldBe(100d);
        analyzer.WindowsFullyDrained.ShouldBe(1);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StartMs.ShouldBe(1_001);
        window.EndMs.ShouldBe(5_001);
        window.StacksGranted.ShouldBe(4);
        window.StacksConsumed.ShouldBe(4);
        window.StacksWasted.ShouldBe(0);
        window.ClosedByExpiry.ShouldBeFalse();
        window.DurationMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task WindowExpiringWithTwoStacksLeft_CountsThemAsWasted()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 3),
            Multishot(3_000),
            RemoveBuffStack(3_002, stack: 2),
            RemoveBuff(13_000));

        analyzer.EmpoweredMultishotCasts.ShouldBe(2);
        analyzer.StacksGranted.ShouldBe(4);
        analyzer.StacksConsumed.ShouldBe(2);
        analyzer.StacksWasted.ShouldBe(2);
        analyzer.WindowsFullyDrained.ShouldBe(0);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StacksConsumed.ShouldBe(2);
        window.StacksWasted.ShouldBe(2);
        window.ClosedByExpiry.ShouldBeTrue();
        window.EndMs.ShouldBe(13_000);
    }

    [Fact]
    public async Task MultishotOutsideAnyWindow_CountsAsARegularCast()
    {
        var analyzer = await Analyze(
            Multishot(1_000),
            Multishot(2_000));

        analyzer.MultishotCasts.ShouldBe(2);
        analyzer.EmpoweredMultishotCasts.ShouldBe(0);
        analyzer.RegularMultishotCasts.ShouldBe(2);
        analyzer.Windows.ShouldBeEmpty();
        analyzer.StacksGranted.ShouldBe(0);
        analyzer.StacksConsumedPercentage.ShouldBe(0d);
    }

    [Fact]
    public async Task MultishotAfterTheWindowClosed_CountsAsARegularCast()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 3),
            RemoveBuff(16_000),
            Multishot(17_000));

        analyzer.MultishotCasts.ShouldBe(2);
        analyzer.EmpoweredMultishotCasts.ShouldBe(1);
        analyzer.RegularMultishotCasts.ShouldBe(1);
        analyzer.StacksWasted.ShouldBe(3);
    }

    [Fact]
    public async Task MultishotWhileTheWindowIsOpenButEmpty_CountsAsARegularCast()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 0),
            Multishot(3_000));

        analyzer.MultishotCasts.ShouldBe(2);
        analyzer.EmpoweredMultishotCasts.ShouldBe(1);
        analyzer.RegularMultishotCasts.ShouldBe(1);
        analyzer.StacksGranted.ShouldBe(1);
        analyzer.StacksConsumed.ShouldBe(1);
        analyzer.StacksWasted.ShouldBe(0);
    }

    [Fact]
    public async Task WindowNeverRemoved_ClosesAtPullEndWithItsStacksWasted()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 3));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StacksConsumed.ShouldBe(1);
        window.StacksWasted.ShouldBe(3);
        window.ClosedByExpiry.ShouldBeTrue();
        window.EndMs.ShouldBe(analyzer.Pull.EndTime);
        analyzer.PullDurationMs.ShouldBe(40_000);
    }

    [Fact]
    public async Task ApplyBuffWithoutTheStackEvent_OpensASingleStackWindow()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            Multishot(2_000),
            RemoveBuff(2_001));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.StacksGranted.ShouldBe(1);
        window.StacksConsumed.ShouldBe(1);
        window.StacksWasted.ShouldBe(0);
        window.ClosedByExpiry.ShouldBeFalse();
    }

    [Fact]
    public async Task RemovalFarFromAnyMultishot_IsNotClaimedAsASpend()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(9_000, stack: 3),
            RemoveBuff(16_000));

        analyzer.StacksConsumed.ShouldBe(0);
        analyzer.StacksWasted.ShouldBe(4);
        analyzer.EmpoweredMultishotCasts.ShouldBe(1);
    }

    [Fact]
    public async Task TwoWindows_AreTrackedSeparately()
    {
        var analyzer = await Analyze(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 3),
            RemoveBuff(16_000),
            SupremacyCast(20_000),
            ApplyBuff(20_001),
            ApplyBuffStack(20_001, stack: 4),
            Multishot(21_000),
            RemoveBuffStack(21_002, stack: 3),
            Multishot(22_000),
            RemoveBuffStack(22_002, stack: 2),
            RemoveBuff(35_000));

        analyzer.SupremacyCasts.ShouldBe(2);
        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].StacksConsumed.ShouldBe(1);
        analyzer.Windows[1].StacksConsumed.ShouldBe(2);
        analyzer.StacksGranted.ShouldBe(8);
        analyzer.StacksConsumed.ShouldBe(3);
        analyzer.StacksWasted.ShouldBe(5);
    }

    [Fact]
    public async Task FerventSupremacy_IsReportedFromTheTalentBuildWithoutGatingTheAnalyzer()
    {
        var withoutTalent = await Analyze(SupremacyCast(1_000));
        withoutTalent.TalentedFerventSupremacy.ShouldBeFalse();

        var (parser, _) = await AnalyzeAsync(FerventSupremacyBuild(), SupremacyCast(1_000));
        parser.SupremacyWindowAnalyzers.ShouldHaveSingleItem()
            .Analyzer.TalentedFerventSupremacy.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_SupremacyWindows_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            SupremacyCast(1_000),
            ApplyBuff(1_001),
            ApplyBuffStack(1_001, stack: 4),
            Multishot(2_000),
            RemoveBuffStack(2_002, stack: 3));

        var entry = parser.SupremacyWindowAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.SupremacyWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).SupremacyWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CombatantInfoEvent FerventSupremacyBuild() => new()
    {
        Timestamp = 0,
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = Talents.FerventSupremacy.FSLID }],
    };

    private static CastEvent SupremacyCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Activation = true,
        Ability = new SpellAbility
        {
            FSLID = Spells.SkystridersSupremacy.FSLID,
            Name = Spells.SkystridersSupremacy.Name,
        },
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

    private static async Task<SupremacyWindowAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(events);
        return parser.SupremacyWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
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
        var result = await parser.Analyze([.. events], PlayerId, Fight);
        return (parser, result);
    }
}
