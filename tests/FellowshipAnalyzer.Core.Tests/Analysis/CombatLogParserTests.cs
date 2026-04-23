using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using NSubstitute;

using static FellowshipAnalyzer.Core.Analysis.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class CombatLogParserTests
{
    [Fact]
    public async Task Analyze_ShouldTrackCastsAndNotify()
    {
        var spellUsable = new SpellUsable();
        var probe = new ProbeModule(spellUsable);
        var owner = CreateCombatLogParser(modules: [spellUsable, probe]);

        await owner.Analyze(CreateEvents(), playerId: 7, fightStartTime: 0);

        Assert.Equal(6, probe.SeenCastCount);
        Assert.Equal(6, probe.ListenerCastCount);
        Assert.Same(spellUsable, probe.State);
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

        var tracker = new TestResourceTracker();
        var owner = CreateCombatLogParser(modules: [tracker]);

        await owner.Analyze(events, playerId: 7, fightStartTime: 0);

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

        var tracker = new TestResourceTracker();
        var owner = CreateCombatLogParser(modules: [tracker]);

        await owner.Analyze(events, playerId: 7, fightStartTime: 0);
        Assert.Equal(1, tracker.GetGenerated(ResourceTypes.Primary));
        Assert.Equal(5, tracker.GetGenerated(ResourceTypes.Secondary));
        Assert.Equal(1, tracker.GetCurrent(ResourceTypes.Primary));
        Assert.Equal(5, tracker.GetCurrent(ResourceTypes.Secondary));
    }

    [Fact]
    public async Task Listener_ShouldFilterBySpellId()
    {
        var spellUsable = new SpellUsable();
        var probe = new SpellFilterProbeModule();
        var owner = CreateCombatLogParser(modules: [spellUsable, probe]);

        await owner.Analyze(CreateEvents(), playerId: 7, fightStartTime: 0);

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

        var spellUsable = new SpellUsable();
        var probe = new ProbeModule(spellUsable);
        var owner = CreateCombatLogParser(modules: [spellUsable, probe]);

        await owner.Analyze(events, playerId: 7, fightStartTime: 0);

        Assert.Equal(2, probe.ListenerCastCount);
    }

    [Fact]
    public async Task FabricateEvent_ShouldMarkAsFabricated()
    {
        var probe = new ProbeModule(new SpellUsable());
        var owner = CreateCombatLogParser(modules: [probe]);

        await owner.Analyze(new List<Event>(), playerId: 7, fightStartTime: 0);

        // Fabricate a cast event after the owner has run (outside dispatch)
        var fabricated = owner.EventEmitter.FabricateEvent(CreateCast(timestamp: 500, abilityId: 1));

        Assert.True(fabricated.Fabricated);
        Assert.Equal(0, probe.ListenerCastCount);
    }

    [Fact]
    public void FormatTimestamp_ShouldReturnFightRelativeTime()
    {
        var owner = CreateCombatLogParser();
        owner.FightStartTime = 1_741_000;

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

        var fabricator = new FabricatingProbeModule();
        var owner = CreateCombatLogParser(modules: [fabricator]);

        await owner.Analyze(events, playerId: 7, fightStartTime: 0);

        // The original event + the fabricated event should both be processed
        Assert.Equal(2, fabricator.TotalCalls);
        Assert.True(fabricator.FabricatedEventWasDeferred);
    }

    private static TestCombatLogParser CreateCombatLogParser(Module[]? modules = null)
    {
        modules ??= [];
        var emitter = new EventEmitter(Microsoft.Extensions.Logging.Abstractions.NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        foreach (var m in modules)
            provider.GetService(m.GetType()).Returns(m);
        return new TestCombatLogParser(emitter, provider, modules);
    }

    private sealed class TestCombatLogParser(EventEmitter emitter, IServiceProvider provider, Module[] modules)
        : CombatLogParser(emitter, provider)
    {
        public override string HeroId => "test";
        protected override Type[] GetModuleTypes() => [.. modules.Select(m => m.GetType())];
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
                Guid = abilityId,
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
            Ability = new Ability { Guid = abilityId, Name = $"Spell {abilityId}" },
        };
    }

    private sealed class ProbeModule(SpellUsable state) : Analyzer
    {
        public SpellUsable State { get; } = state;

        public int SeenCastCount { get; private set; }

        public int ListenerCastCount { get; private set; }

        public override void Initialize()
        {
            AddEventListener(Cast.By(SELECTED_PLAYER), OnCast);
        }

        private void OnCast(CastEvent e)
        {
            ListenerCastCount += 1;
        }

        public override void Complete()
        {
            SeenCastCount = State.Casts.Count;
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

    private sealed class SpellFilterProbeModule : Analyzer
    {
        public int MatchedCastCount { get; private set; }

        public override void Initialize()
        {
            AddEventListener(Cast.By(SELECTED_PLAYER).Spell(new Spell(2)), OnSpender);
        }

        private void OnSpender(CastEvent e)
        {
            MatchedCastCount += 1;
        }
    }

    private sealed class TestResourceTracker : ResourceTracker { }

    private sealed class FabricatingProbeModule : Analyzer
    {
        public int TotalCalls { get; private set; }
        public bool FabricatedEventWasDeferred { get; private set; }
        private bool _alreadyFabricated;

        public override void Initialize()
        {
            AddEventListener(Cast.By(SELECTED_PLAYER), OnCast);
        }

        private void OnCast(CastEvent e)
        {
            TotalCalls++;

            if (!_alreadyFabricated)
            {
                _alreadyFabricated = true;
                // Fabricate during dispatch — should be deferred
                Owner.EventEmitter.FabricateEvent(new CastEvent
                {
                    Timestamp = e.Timestamp + 50,
                    SourceId = 7,
                    TargetId = 11,
                    Ability = new Ability { Guid = 99, Name = "Fabricated" },
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