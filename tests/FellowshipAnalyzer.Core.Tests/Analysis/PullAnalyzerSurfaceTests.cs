using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// The M3 placeholder re-expressed through the M4 attribute surface (<c>[AddState]</c> /
/// <c>[AddAnalyzer]</c> / <c>[ForPull]</c>), proving the generator emits a working per-pull
/// pipeline and the three typed read paths.
/// </summary>
public sealed class PullAnalyzerSurfaceTests
{
    [Fact]
    public async Task GeneratedSurface_ProducesPerPullResults_AcrossAllReadPaths()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddPullSurfaceAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<PullSurfaceCombatLogParser>();

        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 100, EndTime: 300, Name: "P0", EnemyNpcs: null),
            new(Id: 2, EncounterId: 42, Kill: true, StartTime: 500, EndTime: 700, Name: "P1", EnemyNpcs: null),
        };
        var events = new List<Event>
        {
            Buff(150), Buff(250),               // pull 0
            Buff(400),                          // gap — no pull open
            Buff(550), Buff(600), Buff(650),    // pull 1
        };

        var result = await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        // Cross-pull index: parser.{Result}s
        Assert.Equal(2, parser.PullBuffResults.Count);
        var (pull0, r0) = (parser.PullBuffResults[0].Pull, parser.PullBuffResults[0].Result);
        var (pull1, r1) = (parser.PullBuffResults[1].Pull, parser.PullBuffResults[1].Result);
        Assert.Equal(0, pull0.Index);
        Assert.Equal(1, pull1.Index);
        Assert.Equal(2, r0.PullCount);
        Assert.Equal(3, r1.PullCount);
        Assert.Equal(2, r0.FightCountAtEnd);
        Assert.Equal(6, r1.FightCountAtEnd);

        // Per-pull extension property: pull.{Result}
        Assert.Equal(2, pull0.PullBuffResult!.PullCount);
        Assert.Equal(3, pull1.PullBuffResult!.PullCount);

        // Per-pull view: parser.For(pull).{Result}
        Assert.Equal(2, parser.For(pull0).PullBuffResult!.PullCount);
        Assert.Equal(3, parser.For(pull1).PullBuffResult!.PullCount);

        // Untyped base list still populated.
        Assert.Equal(2, parser.PullResults.Count);

        // Typed report carries the analyzer result list.
        var typed = Assert.IsType<PullSurfaceAnalysisResult>(result.TypedReport);
        Assert.Equal(2, typed.PullBuffResults.Count);
    }

    private static ReportFight Fight(IReadOnlyList<DungeonPull> pulls)
        => new(Id: 0, Name: "Fight", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 1000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null, InProgress: false,
            DungeonPulls: pulls);

    private static ApplyBuffEvent Buff(int timestamp)
        => new() { Timestamp = timestamp, SourceId = 7, TargetId = 7 };
}

[AddState<FightBuffCounter>]
[AddAnalyzer<PullBuffAnalyzer>]
public sealed partial class PullSurfaceCombatLogParser : CombatLogParser { }

public sealed partial class FightBuffCounter : EventSubscriber
{
    public int Count { get; private set; }

    [On<ApplyBuffEvent>(By = Actor.Player)]
    private void OnBuff(ApplyBuffEvent e) => Count++;
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class PullBuffAnalyzer(Lazy<FightBuffCounter> state) : Analyzer<PullBuffResult>
{
    private int _count;

    [On<ApplyBuffEvent>(By = Actor.Player)]
    private void OnBuff(ApplyBuffEvent e) => _count++;

    public override PullBuffResult OnPullEnd() => new(_count, state.Value.Count);
}

public sealed record PullBuffResult(int PullCount, int FightCountAtEnd) : IResult
{
    public PullBuffResult() : this(0, 0) { }
}
