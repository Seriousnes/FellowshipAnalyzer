using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class PullLifecycleTests
{
    [Fact]
    public async Task Analyze_YieldsOneResultPerPull_FreshInstance_WithGapIsolation()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 100, EndTime: 300, Name: "P0", EnemyNpcs: null),
            new(Id: 2, EncounterId: 42, Kill: true, StartTime: 500, EndTime: 700, Name: "P1", EnemyNpcs: null),
        };
        var events = new List<Event>
        {
            Buff(150), Buff(250),               // inside pull 0
            Buff(400),                          // in the gap between pulls — no pull open
            Buff(550), Buff(600), Buff(650),    // inside pull 1
        };

        var parser = CreateParser();
        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        Assert.Equal(2, parser.PullResults.Count);

        var (pull0, result0) = parser.PullResults[0];
        var (pull1, result1) = parser.PullResults[1];
        Assert.Equal(0, pull0.Index);
        Assert.Equal(1, pull1.Index);
        Assert.Equal(2, ((PullProbeResult)result0).Count);
        Assert.Equal(3, ((PullProbeResult)result1).Count);

        Assert.Equal(2, ((PullProbeResult)result0).StateCount);
        Assert.Equal(6, ((PullProbeResult)result1).StateCount);

        var state = parser.GetModule<WholeFightCounter>()!;
        Assert.Equal(6, state.Count);
    }

    [Fact]
    public async Task Analyze_ContiguousPulls_NeverTwoOpen_DiscardsCorrectInstance()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 100, EndTime: 300, Name: "A", EnemyNpcs: null),
            new(Id: 2, EncounterId: 42, Kill: true, StartTime: 300, EndTime: 500, Name: "B", EnemyNpcs: null),
        };
        var events = new List<Event>
        {
            Buff(150), Buff(200),   // pull A window
            Buff(300),              // shared boundary timestamp (A.End == B.Start)
            Buff(400),              // pull B window
        };

        var parser = CreateParser();
        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        Assert.Equal(2, parser.PullResults.Count);

        var (pullA, resultA) = parser.PullResults[0];
        var (pullB, resultB) = parser.PullResults[1];
        Assert.Equal("A", pullA.Name);
        Assert.Equal("B", pullB.Name);
        Assert.Equal(2, ((PullProbeResult)resultA).Count);
        Assert.Equal(2, ((PullProbeResult)resultB).Count);

        var state = parser.GetModule<WholeFightCounter>()!;
        Assert.Equal(4, state.Count);
    }

    private static PullTestParser CreateParser()
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        return new PullTestParser(emitter, provider);
    }

    private static ReportFight Fight(IReadOnlyList<DungeonPull> pulls)
        => new(Id: 0, Name: "Fight", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 1000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null, InProgress: false,
            DungeonPulls: pulls);

    private static ApplyBuffEvent Buff(int timestamp, int sourceId = 7)
        => new() { Timestamp = timestamp, SourceId = sourceId, TargetId = 7 };

    private sealed class PullTestParser(EventEmitter emitter, IServiceProvider provider)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [typeof(WholeFightCounter)];

        protected override Type[] GetNormalizerTypes() =>
            [typeof(FightBookendNormalizer), typeof(PullBookendNormalizer)];

        protected override Type[] GetAnalyzerTypes() => [typeof(PullProbeAnalyzer)];

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(WholeFightCounter)) return new WholeFightCounter();
            if (type == typeof(PullProbeAnalyzer))
                return new PullProbeAnalyzer(
                    new Lazy<WholeFightCounter>(() => (WholeFightCounter)ResolveAnalysisModule(typeof(WholeFightCounter))));
            return base.CreateInstance(type);
        }
    }

    private sealed partial class WholeFightCounter : Analyzer
    {
        public int Count { get; private set; }

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) => Count++;
    }

    private sealed partial class PullProbeAnalyzer(Lazy<WholeFightCounter> state) : Analyzer<PullProbeResult>
    {
        private int _count;

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) => _count++;

        public override PullProbeResult OnPullEnd() => new(_count, state.Value.Count);
    }

    private sealed record PullProbeResult(int Count, int StateCount) : IResult
    {
        public PullProbeResult() : this(0, 0) { }
    }
}
