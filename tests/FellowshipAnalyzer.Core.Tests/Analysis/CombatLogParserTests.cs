using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class CombatLogParserTests
{
    [Fact]
    public async Task Analyze_ShouldTrackCastsAndNotify()
    {
        var owner = CreateCombatLogParser([typeof(StatTracker), typeof(SpellUsable), typeof(ProbeModule)]);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);

        var spellUsable = owner.GetModule<SpellUsable>()!;
        var probe = owner.GetModule<ProbeModule>()!;
        Assert.Equal(6, probe.SeenCastCount);
        Assert.Equal(6, probe.ListenerCastCount);
        Assert.Same(spellUsable, probe.State);
    }

    [Fact]
    public async Task Analyze_WhenReused_ShouldNotAccumulateListeners()
    {
        var owner = CreateCombatLogParser([typeof(CountingProbeModule)]);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);
        var firstProbe = owner.GetModule<CountingProbeModule>()!;
        Assert.Equal(6, firstProbe.ListenerCastCount);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);
        var secondProbe = owner.GetModule<CountingProbeModule>()!;
        Assert.Equal(6, secondProbe.ListenerCastCount);
        Assert.NotSame(firstProbe, secondProbe);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);
        var thirdProbe = owner.GetModule<CountingProbeModule>()!;
        Assert.Equal(6, thirdProbe.ListenerCastCount);
        Assert.NotSame(secondProbe, thirdProbe);
    }

    [Fact]
    public async Task Analyze_WhenReused_ShouldUseFreshCombatants()
    {
        const int buffId = 42;
        var events = new List<Event>
        {
            CreateCombatantInfo(sourceId: 7),
            CreateApplyBuff(timestamp: 100, abilityId: buffId),
            CreateRemoveBuff(timestamp: 200, abilityId: buffId),
        };

        var owner = CreateCombatLogParser([typeof(Combatants)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var firstCombatants = owner.GetModule<Combatants>()!;
        AssertSingleTrackedBuff(firstCombatants, buffId);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var secondCombatants = owner.GetModule<Combatants>()!;
        AssertSingleTrackedBuff(secondCombatants, buffId);
        Assert.NotSame(firstCombatants, secondCombatants);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var thirdCombatants = owner.GetModule<Combatants>()!;
        AssertSingleTrackedBuff(thirdCombatants, buffId);
        Assert.NotSame(secondCombatants, thirdCombatants);
    }

    [Fact]
    public async Task Analyze_WhenReused_ShouldUseFreshResourceTracker()
    {
        var events = new List<Event>
        {
            CreateResourceChange(timestamp: 100, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 0),
            CreateResourceChange(timestamp: 200, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 0),
            CreateResourceChange(timestamp: 300, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 1),
        };

        var owner = CreateCombatLogParser([typeof(TestResourceTracker)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var firstTracker = owner.GetModule<TestResourceTracker>()!;
        Assert.Equal(2, firstTracker.GetGenerated(ResourceTypes.Primary));
        Assert.Equal(1, firstTracker.GetWasted(ResourceTypes.Primary));

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var secondTracker = owner.GetModule<TestResourceTracker>()!;
        Assert.Equal(2, secondTracker.GetGenerated(ResourceTypes.Primary));
        Assert.Equal(1, secondTracker.GetWasted(ResourceTypes.Primary));
        Assert.NotSame(firstTracker, secondTracker);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var thirdTracker = owner.GetModule<TestResourceTracker>()!;
        Assert.Equal(2, thirdTracker.GetGenerated(ResourceTypes.Primary));
        Assert.Equal(1, thirdTracker.GetWasted(ResourceTypes.Primary));
        Assert.NotSame(secondTracker, thirdTracker);
    }

    [Fact]
    public async Task Analyze_ShouldShareInjectedModuleWithinAnalysisRun()
    {
        var owner = CreateCombatLogParser([typeof(StatTracker), typeof(SpellUsable), typeof(ProbeModule)]);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);
        var spellUsable = owner.GetModule<SpellUsable>()!;
        var probe = owner.GetModule<ProbeModule>()!;
        Assert.Same(spellUsable, probe.State);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);
        var nextSpellUsable = owner.GetModule<SpellUsable>()!;
        var nextProbe = owner.GetModule<ProbeModule>()!;
        Assert.Same(nextSpellUsable, nextProbe.State);
        Assert.NotSame(spellUsable, nextSpellUsable);
    }

    [Fact]
    public async Task ResourceTracker_ShouldTrackGainsAndSpends()
    {
        var events = new List<Event>
        {
            CreateResourceChange(timestamp: 100, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 0),
            CreateResourceChange(timestamp: 200, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 0),
            CreateResourceChange(timestamp: 300, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 1),
            CreateCast(timestamp: 400, abilityId: 2, classResources: [new ClassResource { Type = ResourceTypes.Primary, Amount = 2, Max = 2, Cost = 2 }]),
            CreateResourceChange(timestamp: 500, abilityId: 3, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 0),
            CreateCast(timestamp: 900, abilityId: 2, classResources: [new ClassResource { Type = ResourceTypes.Primary, Amount = 1, Max = 2, Cost = 2 }]),
        };

        var owner = CreateCombatLogParser([typeof(TestResourceTracker)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var tracker = owner.GetModule<TestResourceTracker>()!;
        Assert.Equal(3, tracker.GetGenerated(ResourceTypes.Primary));
        Assert.Equal(1, tracker.GetWasted(ResourceTypes.Primary));
        Assert.Equal(4, tracker.GetSpent(ResourceTypes.Primary));
        Assert.Equal(0, tracker.GetCurrent(ResourceTypes.Primary));
        Assert.Equal(3, tracker.GetGeneratorCasts(ResourceTypes.Primary)[1]);
        Assert.Equal(1, tracker.GetGeneratorCasts(ResourceTypes.Primary)[3]);
        Assert.Equal(2, tracker.GetSpenderCasts(ResourceTypes.Primary)[2]);
    }

    [Fact]
    public async Task ResourceTracker_ShouldTrackAllResourceTypes()
    {
        var events = new List<Event>
        {
            CreateResourceChange(timestamp: 100, abilityId: 1, resourceType: ResourceTypes.Primary, resourceChange: 1, waste: 0),
            CreateResourceChange(timestamp: 200, abilityId: 1, resourceType: ResourceTypes.Secondary, resourceChange: 5, waste: 0),
        };

        var owner = CreateCombatLogParser([typeof(TestResourceTracker)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);
        var tracker = owner.GetModule<TestResourceTracker>()!;
        Assert.Equal(1, tracker.GetGenerated(ResourceTypes.Primary));
        Assert.Equal(5, tracker.GetGenerated(ResourceTypes.Secondary));
        Assert.Equal(1, tracker.GetCurrent(ResourceTypes.Primary));
        Assert.Equal(5, tracker.GetCurrent(ResourceTypes.Secondary));
    }

    [Fact]
    public async Task Listener_ShouldFilterBySpellId()
    {
        var owner = CreateCombatLogParser([typeof(StatTracker), typeof(SpellUsable), typeof(SpellFilterProbeModule)]);

        await owner.Analyze(CreateEvents(), playerId: 7, dungeon: TestDungeon);

        var probe = owner.GetModule<SpellFilterProbeModule>()!;
        Assert.Equal(2, probe.MatchedCastCount);
    }

    [Fact]
    public async Task Listener_ShouldIgnoreEventsFromOtherPlayers()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: 100, abilityId: 1),
            CreateCast(timestamp: 200, abilityId: 1, sourceId: 99),
            CreateCast(timestamp: 300, abilityId: 1),
        };

        var owner = CreateCombatLogParser([typeof(StatTracker), typeof(SpellUsable), typeof(ProbeModule)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var probe = owner.GetModule<ProbeModule>()!;
        Assert.Equal(2, probe.ListenerCastCount);
    }

    [Fact]
    public async Task FabricateEvent_ShouldMarkAsFabricated()
    {
        var owner = CreateCombatLogParser([typeof(StatTracker), typeof(SpellUsable), typeof(ProbeModule)]);

        await owner.Analyze(new List<Event>(), playerId: 7, dungeon: TestDungeon);

        var fabricated = owner.EventEmitter.FabricateEvent(CreateCast(timestamp: 500, abilityId: 1));

        var probe = owner.GetModule<ProbeModule>()!;
        Assert.True(fabricated.Fabricated);
        Assert.Equal(0, probe.ListenerCastCount);
    }

    [Fact]
    public async Task FormatTimestamp_ShouldReturnDungeonRelativeTime()
    {
        var owner = CreateCombatLogParser();
        await owner.Analyze(
            [],
            playerId: 7,
            dungeon: new ReportDungeon(0, "", 0, null, StartTime: 1_741_000, EndTime: 0, null, null, null));

        var formatted = owner.FormatTimestamp(1_744_535);

        Assert.Equal("0:03", formatted);
    }

    [Fact]
    public async Task FabricateEvent_ShouldDeferDuringDispatch()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: 100, abilityId: 1),
        };

        var owner = CreateCombatLogParser([typeof(FabricatingProbeModule)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var fabricator = owner.GetModule<FabricatingProbeModule>()!;
        Assert.Equal(2, fabricator.TotalCalls);
        Assert.True(fabricator.FabricatedEventWasDeferred);
    }

    [Fact]
    public async Task FabricateEvent_WithFutureTimestamp_InsertsInTimestampOrder()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: 100, abilityId: 1),
            CreateCast(timestamp: 200, abilityId: 1),
        };

        var owner = CreateCombatLogParser([typeof(FutureFabricatingProbeModule)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var stream = owner.Events;
        var fabricatedIndex = stream.FindIndex(e =>
            e is CastEvent c && c.Ability.Id == FutureFabricatingProbeModule.FabricatedAbilityId);
        var laterCastIndex = stream.FindIndex(e =>
            e is CastEvent c && c.Ability.Id == 1 && c.Timestamp == 200);

        Assert.True(fabricatedIndex > laterCastIndex,
            "a fabricated future event must be inserted in timestamp order, after the later real cast");
    }

    [Fact]
    public async Task FabricateEvent_WithEarlierTimestamp_ClampsToCurrentTimestamp()
    {
        var events = new List<Event> { CreateCast(timestamp: 200, abilityId: 1) };

        var owner = CreateCombatLogParser([typeof(EarlierFabricatingProbeModule)]);

        await owner.Analyze(events, playerId: 7, dungeon: TestDungeon);

        var probe = owner.GetModule<EarlierFabricatingProbeModule>()!;
        Assert.NotNull(probe.Fabricated);
        Assert.Equal(200, probe.Fabricated!.Timestamp);
    }

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static TestCombatLogParser CreateCombatLogParser(Type[]? moduleTypes = null, Type[]? normalizerTypes = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        provider.GetService(typeof(ILogger<ResourceTracker>)).Returns(NullLogger<ResourceTracker>.Instance);
        return new TestCombatLogParser(emitter, provider, moduleTypes ?? [], normalizerTypes ?? [typeof(DungeonBookendNormalizer)]);
    }

    private sealed class TestCombatLogParser(
        EventEmitter emitter,
        IServiceProvider provider,
        Type[] moduleTypes,
        Type[] normalizerTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;
        protected override Type[] GetNormalizerTypes() => normalizerTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(ProbeModule))
                return new ProbeModule((SpellUsable)ResolveAnalysisModule(typeof(SpellUsable)));
            if (type == typeof(CountingProbeModule))
                return new CountingProbeModule();
            if (type == typeof(SpellFilterProbeModule))
                return new SpellFilterProbeModule();
            if (type == typeof(FabricatingProbeModule))
                return new FabricatingProbeModule();
            if (type == typeof(FutureFabricatingProbeModule))
                return new FutureFabricatingProbeModule();
            if (type == typeof(EarlierFabricatingProbeModule))
                return new EarlierFabricatingProbeModule();
            if (type == typeof(TestResourceTracker))
                return new TestResourceTracker(NullLogger<ResourceTracker>.Instance);
            return base.CreateInstance(type);
        }
    }

    private static List<Event> CreateEvents()
    {
        return
        [
            CreateCast(timestamp: 100, abilityId: 1),
            CreateCast(timestamp: 200, abilityId: 1),
            CreateCast(timestamp: 300, abilityId: 1),
            CreateCast(timestamp: 400, abilityId: 2),
            CreateCast(timestamp: 500, abilityId: 3),
            CreateCast(timestamp: 900, abilityId: 2),
        ];
    }

    private static CastEvent CreateCast(int timestamp, int abilityId, int sourceId = 7, List<ClassResource>? classResources = null)
    {
        return new CastEvent
        {
            Timestamp = timestamp,
            SourceId = sourceId,
            TargetId = 11,
            Ability = new Ability
            {
                FSLID = abilityId,
                Name = $"Spell {abilityId}",
            },
            Target = new CastTarget(),
            Channel = new EndChannelEvent(),
            SourceResources = classResources is null ? null : new ActorResources { Resources = classResources },
        };
    }

    private static ResourceChangeEvent CreateResourceChange(int timestamp, int abilityId, ResourceTypes resourceType, double resourceChange, double waste, int sourceId = 7)
    {
        return new ResourceChangeEvent
        {
            Timestamp = timestamp,
            SourceId = sourceId,
            TargetId = 11,
            ResourceChangeType = resourceType,
            ResourceChange = resourceChange,
            Waste = waste,
            Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        };
    }

    private static CombatantInfoEvent CreateCombatantInfo(int sourceId) => new()
    {
        SourceId = sourceId,
    };

    private static ApplyBuffEvent CreateApplyBuff(int timestamp, int abilityId, int sourceId = 7, int targetId = 7) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = targetId,
        Ability = new Ability { FSLID = abilityId, Name = $"Buff {abilityId}" },
    };

    private static RemoveBuffEvent CreateRemoveBuff(int timestamp, int abilityId, int sourceId = 7, int targetId = 7) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = targetId,
        Ability = new Ability { FSLID = abilityId, Name = $"Buff {abilityId}" },
    };

    private static void AssertSingleTrackedBuff(Combatants combatants, int abilityId)
    {
        var buff = Assert.Single(combatants.GetCombatant(7, null)!.Buffs);
        Assert.Equal(abilityId, buff.Ability.FSLID.Value);
        Assert.Equal(100, buff.Start);
        Assert.Equal(200, buff.End);
    }

    private sealed partial class CountingProbeModule : Analyzer
    {
        public int ListenerCastCount { get; private set; }

        [On<CastEvent>(By = Actor.Player)]
        private void OnCast(CastEvent e)
        {
            ListenerCastCount += 1;
        }
    }

    private sealed partial class ProbeModule(SpellUsable state) : Analyzer
    {
        public SpellUsable State { get; } = state;

        public int SeenCastCount { get; private set; }

        public int ListenerCastCount { get; private set; }

        [On<CastEvent>(By = Actor.Player)]
        private void OnCast(CastEvent e)
        {
            ListenerCastCount += 1;
        }

        [On<DungeonEndEvent>]
        private void OnDungeonEnd(DungeonEndEvent e)
        {
            SeenCastCount = State.Casts.Count;
        }
    }

    private sealed partial class FutureFabricatingProbeModule : Analyzer
    {
        public const int FabricatedAbilityId = 99;
        private bool _fabricated;

        [On<CastEvent>(By = Actor.Player)]
        private void OnCast(CastEvent e)
        {
            if (_fabricated || e.Ability.Id == FabricatedAbilityId) return;
            _fabricated = true;
            Owner.EventEmitter.FabricateEvent(new CastEvent
            {
                Timestamp = 1000,
                SourceId = 7,
                TargetId = 11,
                Ability = new Ability { FSLID = FabricatedAbilityId, Name = "Future" },
                Target = new CastTarget(),
                Channel = new EndChannelEvent(),
            });
        }
    }

    private sealed partial class EarlierFabricatingProbeModule : Analyzer
    {
        public const int FabricatedAbilityId = 98;
        public CastEvent? Fabricated { get; private set; }
        private bool _fabricated;

        [On<CastEvent>(By = Actor.Player)]
        private void OnCast(CastEvent e)
        {
            if (_fabricated || e.Ability.Id == FabricatedAbilityId) return;
            _fabricated = true;
            Fabricated = Owner.EventEmitter.FabricateEvent(new CastEvent
            {
                Timestamp = 50,
                SourceId = 7,
                TargetId = 11,
                Ability = new Ability { FSLID = FabricatedAbilityId, Name = "Earlier" },
                Target = new CastTarget(),
                Channel = new EndChannelEvent(),
            });
        }
    }

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;

        public int Id { get; set; }

        public int Guid { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;
    }

    private sealed class TestSpells : ISpellRegistry
    {
        public static Spell Spender { get; } = new Spell { Id = 2 };
    }

    private sealed partial class SpellFilterProbeModule : Analyzer
    {
        public int MatchedCastCount { get; private set; }

        [On<CastEvent>(By = Actor.Player, Spell = nameof(TestSpells.Spender))]
        private void OnSpender(CastEvent e)
        {
            MatchedCastCount += 1;
        }
    }

    private sealed class TestResourceTracker(ILogger<ResourceTracker> logger) : ResourceTracker(logger) { }

    private sealed partial class FabricatingProbeModule : Analyzer
    {
        public int TotalCalls { get; private set; }
        public bool FabricatedEventWasDeferred { get; private set; }
        private bool _alreadyFabricated;

        [On<CastEvent>(By = Actor.Player)]
        private void OnCast(CastEvent e)
        {
            TotalCalls++;

            if (!_alreadyFabricated)
            {
                _alreadyFabricated = true;
                Owner.EventEmitter.FabricateEvent(new CastEvent
                {
                    Timestamp = e.Timestamp + 50,
                    SourceId = 7,
                    TargetId = 11,
                    Ability = new Ability { FSLID = 99, Name = "Fabricated" },
                    Target = new CastTarget(),
                    Channel = new EndChannelEvent(),
                });
            }
            else if (e.Fabricated == true)
            {
                FabricatedEventWasDeferred = true;
            }
        }
    }
}
