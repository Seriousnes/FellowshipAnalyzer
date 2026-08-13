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
            Buff(150), Buff(250),
            Buff(400),
            Buff(550), Buff(600), Buff(650),
        };

        var parser = CreateParser();
        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        Assert.Equal(2, parser.PullAnalyzers.Count);

        var (pull0, analyzer0) = parser.PullAnalyzers[0];
        var (pull1, analyzer1) = parser.PullAnalyzers[1];
        Assert.Equal(0, pull0.Index);
        Assert.Equal(1, pull1.Index);
        Assert.Equal(2, ((PullProbeAnalyzer)analyzer0).Count);
        Assert.Equal(3, ((PullProbeAnalyzer)analyzer1).Count);

        Assert.Equal(2, ((PullProbeAnalyzer)analyzer0).StateCountAtEnd);
        Assert.Equal(6, ((PullProbeAnalyzer)analyzer1).StateCountAtEnd);

        Assert.Same(analyzer0, pull0.GetAnalyzer(typeof(PullProbeAnalyzer)));
        Assert.Same(analyzer1, pull1.GetAnalyzer(typeof(PullProbeAnalyzer)));

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
            Buff(150), Buff(200),
            Buff(300),
            Buff(400),
        };

        var parser = CreateParser();
        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        Assert.Equal(2, parser.PullAnalyzers.Count);

        var (pullA, analyzerA) = parser.PullAnalyzers[0];
        var (pullB, analyzerB) = parser.PullAnalyzers[1];
        Assert.Equal("A", pullA.Name);
        Assert.Equal("B", pullB.Name);
        Assert.Equal(2, ((PullProbeAnalyzer)analyzerA).Count);
        Assert.Equal(2, ((PullProbeAnalyzer)analyzerB).Count);

        Assert.Equal(2, ((PullProbeAnalyzer)analyzerA).StateCountAtEnd);
        Assert.Equal(4, ((PullProbeAnalyzer)analyzerB).StateCountAtEnd);

        var state = parser.GetModule<WholeFightCounter>()!;
        Assert.Equal(4, state.Count);
    }

    [Fact]
    public async Task Analyze_AnalyzerImplementingTwoSurfaceInterfaces_Throws()
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        var parser = new TwoSurfaceParser(emitter, provider);

        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 100, EndTime: 300, Name: "P0", EnemyNpcs: null),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => parser.Analyze([Buff(150)], playerId: 7, fight: Fight(pulls)));
        Assert.Contains("multiple surface interfaces", ex.Message);
    }

    [Fact]
    public async Task Analyze_PopulatesPulls_InOrder_IncludingAnalyzerlessPulls_AndResetsBetweenRuns()
    {
        var pulls = new List<DungeonPull>
        {
            new(Id: 1, EncounterId: 0, Kill: null, StartTime: 100, EndTime: 300, Name: "P0", EnemyNpcs: null),
            new(Id: 2, EncounterId: 42, Kill: true, StartTime: 500, EndTime: 700, Name: "P1", EnemyNpcs: null),
        };
        var events = new List<Event> { Buff(150), Buff(550) };

        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        var parser = new FirstPullOnlyParser(emitter, provider);

        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        Assert.Equal(2, parser.Pulls.Count);
        Assert.Equal("P0", parser.Pulls[0].Name);
        Assert.Equal("P1", parser.Pulls[1].Name);
        Assert.Single(parser.PullAnalyzers);
        Assert.NotNull(parser.Pulls[0].GetAnalyzer(typeof(PullProbeAnalyzer)));
        Assert.Null(parser.Pulls[1].GetAnalyzer(typeof(PullProbeAnalyzer)));

        await parser.Analyze(events, playerId: 7, fight: Fight(pulls));

        Assert.Equal(2, parser.Pulls.Count);
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
            [typeof(DungeonBookendNormalizer), typeof(PullBookendNormalizer)];

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

    private sealed class FirstPullOnlyParser(EventEmitter emitter, IServiceProvider provider)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [typeof(WholeFightCounter)];

        protected override Type[] GetNormalizerTypes() =>
            [typeof(DungeonBookendNormalizer), typeof(PullBookendNormalizer)];

        protected override Type[] GetAnalyzerTypes(Pull pull) =>
            pull.Index == 0 ? [typeof(PullProbeAnalyzer)] : [];

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

    private sealed partial class PullProbeAnalyzer(Lazy<WholeFightCounter> state) : Analyzer
    {
        public int Count { get; private set; }
        public int StateCountAtEnd { get; private set; }

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) => Count++;

        [On<PullEndEvent>]
        private void OnPullEnd(PullEndEvent _) => StateCountAtEnd = state.Value.Count;
    }

    private interface ISurfaceMarkerA : IAnalyzerSurface;

    private interface ISurfaceMarkerB : IAnalyzerSurface;

    private sealed partial class TwoSurfaceProbe : Analyzer, ISurfaceMarkerA, ISurfaceMarkerB
    {
        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) { }
    }

    private sealed class TwoSurfaceParser(EventEmitter emitter, IServiceProvider provider)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [];

        protected override Type[] GetNormalizerTypes() =>
            [typeof(DungeonBookendNormalizer), typeof(PullBookendNormalizer)];

        protected override Type[] GetAnalyzerTypes() => [typeof(TwoSurfaceProbe)];

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TwoSurfaceProbe)) return new TwoSurfaceProbe();
            return base.CreateInstance(type);
        }
    }
}
