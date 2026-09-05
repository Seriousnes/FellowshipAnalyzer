using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises Chrona Tap over one pull. <c>ResourceNormalizer</c> divides every resource by 100 before
/// dispatch, mana included.
/// </summary>
public sealed class ChronaTapAnalyzerTests
{
    private const int PlayerId = 7;
    private const int AllyId = 9;
    private const int RawMaxMana = 165_600;
    private const int MaxMana = RawMaxMana / 100;
    private const int PullEnd = 60_000;

    private static readonly int ManaPerStack = (int)Math.Round(0.013 * MaxMana);

    [Fact]
    public async Task WithoutTheTalent_TheAnalyzerDoesNotRun()
    {
        var parser = await AnalyzeParser([Combatant(), Spender(1_000)]);

        parser.ChronaTapAnalyzers.ShouldBeEmpty();
    }

    [Fact]
    public async Task EverySpenderCounts_AndStacksAreCountedAsTheyAreGained()
    {
        var analyzer = await Analyze(
            Spender(1_000),
            Applied(1_050),
            Spender(3_000),
            Stacked(3_050, 2),
            Spender(5_000),
            Stacked(5_050, 3));

        analyzer.SpenderCasts.ShouldBe(3);
        analyzer.StacksGained.ShouldBe(3);
        analyzer.StacksPerSpender.ShouldBe(1d, 0.0001);
    }

    [Fact]
    public async Task EveryChronaSpenderIsCounted_AndNothingElseIs()
    {
        var analyzer = await Analyze(
            Cast(1_000, Spells.Oblivion.FSLID),
            Cast(2_000, Spells.AmendFate.FSLID),
            Cast(3_000, Spells.RestoreContinuity.FSLID),
            Cast(4_000, Spells.TimeShard.FSLID),
            Cast(5_000, Spells.UnfoldingDoom.FSLID));

        analyzer.SpenderCasts.ShouldBe(3);
    }

    [Fact]
    public async Task StackHistory_RecordsEveryChange()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 2),
            StackRemoved(3_000, 1),
            Removed(4_000));

        analyzer.StackHistory.Select(sample => sample.Stacks).ShouldBe([1, 2, 1, 0]);
        analyzer.StackHistory.Select(sample => sample.Timestamp).ShouldBe([1_000, 2_000, 3_000, 4_000]);
    }

    [Fact]
    public async Task ManaReturned_CountsEveryStackHeldWhenTheEffectEnds()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 2),
            Stacked(3_000, 3),
            Removed(4_000));

        analyzer.ManaPerStack.ShouldBe(ManaPerStack);
        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task EachStackThatExpiresOnItsOwn_ReturnsItsOwnMana()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 3),
            StackRemoved(3_000, 2),
            StackRemoved(4_000, 1),
            Removed(5_000));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task SeveralStacksLostAtOnce_ReturnEachOfTheirShares()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 4),
            StackRemoved(3_000, 1));

        analyzer.ManaReturned.ShouldBe(3 * ManaPerStack);
    }

    [Fact]
    public async Task ASpenderCastBelowTheCap_LosesNothing()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, 9),
            Spender(3_000));

        analyzer.SpendersAtMaximumStacks.ShouldBe(0);
        analyzer.StacksLostAtCap.ShouldBe(0);
        analyzer.ManaLostAtCap.ShouldBe(0);
        analyzer.Overcaps.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASpenderCastAtTheCap_LosesAStack()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, ChronaTapAnalyzer.MaximumStacks),
            Spender(3_000),
            Spender(4_000));

        analyzer.SpendersAtMaximumStacks.ShouldBe(2);
        analyzer.StacksLostAtCap.ShouldBe(2);
        analyzer.ManaLostAtCap.ShouldBe(2 * ManaPerStack);
        analyzer.SpendersAtMaximumStacksShare.ShouldBe(1d, 0.0001);

        var overcap = analyzer.Overcaps[0];
        overcap.Timestamp.ShouldBe(3_000);
        overcap.AbilityId.ShouldBe(Spells.Oblivion.FSLID.Value);
        overcap.ManaLost.ShouldBe(ManaPerStack);
    }

    [Fact]
    public async Task ASpenderCastAfterTheStacksFallBelowTheCap_LosesNothing()
    {
        var analyzer = await Analyze(
            Applied(1_000),
            Stacked(2_000, ChronaTapAnalyzer.MaximumStacks),
            StackRemoved(3_000, ChronaTapAnalyzer.MaximumStacks - 1),
            Spender(4_000));

        analyzer.SpendersAtMaximumStacks.ShouldBe(0);
        analyzer.SpenderCasts.ShouldBe(1);
        analyzer.SpendersAtMaximumStacksShare.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task TheStackCapMatchesTheExport() =>
        ChronaTapAnalyzer.MaximumStacks.ShouldBe(10);

    [Fact]
    public async Task ThePerStackShareComesFromTheTalentRecord()
    {
        var generation = Talents.ChronaTap.ResourceGeneration.ShouldNotBeNull();

        generation.Amount.ShouldBe(0.013, 0.000001);
        generation.Resource.ShouldBe(ResourceTypes.Mana);

        var analyzer = await Analyze(Applied(1_000), Removed(2_000));
        analyzer.ManaPerStack.ShouldBe(ManaPerStack);
    }

    [Fact]
    public async Task NothingRecorded_ReadsZeroWithoutFailing()
    {
        var analyzer = await Analyze();

        analyzer.SpenderCasts.ShouldBe(0);
        analyzer.StacksGained.ShouldBe(0);
        analyzer.StacksPerSpender.ShouldBe(0d, 0.0001);
        analyzer.ManaReturned.ShouldBe(0);
        analyzer.ManaLostAtCap.ShouldBe(0);
        analyzer.StackHistory.ShouldBeEmpty();
    }

    private static CombatantInfoEvent Combatant(params int[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(id => new TalentInfo { Id = id })],
    };

    private static CastEvent Spender(int timestamp) => Cast(timestamp, Spells.Oblivion.FSLID);

    private static CastEvent Cast(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = AllyId,
        Ability = new Ability { Id = abilityId },
        SourceResources = ManaSnapshot(),
    };

    private static ApplyBuffEvent Applied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(),
    };

    private static ApplyBuffStackEvent Stacked(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(),
    };

    private static RemoveBuffStackEvent StackRemoved(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(),
    };

    private static RemoveBuffEvent Removed(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
        SourceResources = ManaSnapshot(),
    };

    private static ActorResources ManaSnapshot() => new()
    {
        HitPoints = 20_000,
        MaxHitPoints = 30_000,
        Resources = [new ClassResource { Type = ResourceTypes.Mana, Amount = 100_000, Max = RawMaxMana }],
    };

    private static async Task<ChronaTapAnalyzer> Analyze(params Event[] events)
    {
        var parser = await AnalyzeParser([Combatant(AeonaTalents.ChronaTap), .. events]);

        return parser.ChronaTapAnalyzers
            .ShouldHaveSingleItem()
            .Analyzer
            .ShouldBeOfType<ChronaTapAnalyzer>();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeParser(Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "Boss", 1, true, 0, PullEnd, null, null, null));
        return parser;
    }
}
