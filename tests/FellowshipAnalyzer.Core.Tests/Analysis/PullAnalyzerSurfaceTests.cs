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

        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        // Cross-pull index: parser.{Analyzer}s
        Assert.Equal(2, parser.PullBuffAnalyzers.Count);
        var (pull0, a0) = (parser.PullBuffAnalyzers[0].Pull, parser.PullBuffAnalyzers[0].Analyzer);
        var (pull1, a1) = (parser.PullBuffAnalyzers[1].Pull, parser.PullBuffAnalyzers[1].Analyzer);
        Assert.Equal(0, pull0.Index);
        Assert.Equal(1, pull1.Index);
        Assert.Equal(2, a0.PullCount);
        Assert.Equal(3, a1.PullCount);
        Assert.Equal(2, a0.FightCountAtEnd);
        Assert.Equal(6, a1.FightCountAtEnd);

        // Per-pull extension property: pull.{Analyzer}
        Assert.Same(a0, pull0.PullBuffAnalyzer);
        Assert.Same(a1, pull1.PullBuffAnalyzer);

        // Per-pull view: parser.For(pull).{Analyzer}
        Assert.Same(a0, parser.For(pull0).PullBuffAnalyzer);
        Assert.Same(a1, parser.For(pull1).PullBuffAnalyzer);

        // Untyped base list still populated.
        Assert.Equal(2, parser.PullAnalyzers.Count);
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
public sealed partial class PullBuffAnalyzer(Lazy<FightBuffCounter> state) : Analyzer
{
    public int PullCount { get; private set; }
    public int FightCountAtEnd { get; private set; }

    [On<ApplyBuffEvent>(By = Actor.Player)]
    private void OnBuff(ApplyBuffEvent e) => PullCount++;

    public override void OnPullEnd() => FightCountAtEnd = state.Value.Count;
}
