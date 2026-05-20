using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using OneOf;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class MultiOnHandlerTests
{
    private const int PlayerId = 7;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task InterfaceParam_ReceivesAllStackedAttributes()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: 100, abilityId: 1),
            CreateHeal(timestamp: 200, abilityId: 2),
        };

        var owner = CreateCombatLogParser([typeof(InterfaceParamProbe)]);
        await owner.Analyze(events, playerId: PlayerId, fight: TestFight);

        var probe = owner.GetModule<InterfaceParamProbe>()!;
        Assert.Equal(2, probe.Count);
        Assert.Equal(1, probe.CastCount);
        Assert.Equal(1, probe.HealCount);
    }

    [Fact]
    public async Task OneOfParam_DispatchesToCorrectSlot()
    {
        var events = new List<Event>
        {
            CreateCast(timestamp: 100, abilityId: 1),
            CreateHeal(timestamp: 200, abilityId: 2),
        };

        var owner = CreateCombatLogParser([typeof(OneOfParamProbe)]);
        await owner.Analyze(events, playerId: PlayerId, fight: TestFight);

        var probe = owner.GetModule<OneOfParamProbe>()!;
        Assert.Equal(1, probe.T0Hits);
        Assert.Equal(1, probe.T1Hits);
    }

    private static TestCombatLogParser CreateCombatLogParser(Type[] moduleTypes)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(Microsoft.Extensions.Logging.ILogger<EventEmitter>))
            .Returns(NullLogger<EventEmitter>.Instance);
        provider.GetService(typeof(Microsoft.Extensions.Logging.ILogger<ResourceTracker>))
            .Returns(NullLogger<ResourceTracker>.Instance);
        return new TestCombatLogParser(emitter, provider, moduleTypes, [typeof(FightBookendNormalizer)]);
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
            if (type == typeof(InterfaceParamProbe)) return new InterfaceParamProbe();
            if (type == typeof(OneOfParamProbe)) return new OneOfParamProbe();
            return base.CreateInstance(type);
        }
    }

    private static CastEvent CreateCast(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = 11,
        Ability = new Ability { Guid = abilityId, Name = $"Spell {abilityId}" },
        Target = new CastTarget(),
        Channel = new EndChannelEvent(),
    };

    private static HealEvent CreateHeal(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = 11,
        Ability = new Ability { Guid = abilityId, Name = $"Heal {abilityId}" },
        Amount = 10,
    };

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    private sealed partial class InterfaceParamProbe : Analyzer
    {
        public int Count { get; private set; }
        public int CastCount { get; private set; }
        public int HealCount { get; private set; }

        [On<CastEvent>, On<HealEvent>]
        private void OnAny(IAbilityEvent e)
        {
            Count++;
            switch (e)
            {
                case CastEvent: CastCount++; break;
                case HealEvent: HealCount++; break;
            }
        }
    }

    private sealed partial class OneOfParamProbe : Analyzer
    {
        public int T0Hits { get; private set; }
        public int T1Hits { get; private set; }

        [On<CastEvent>, On<HealEvent>]
        private void OnUnion(OneOf<CastEvent, HealEvent> e)
        {
            if (e.IsT0) T0Hits++;
            if (e.IsT1) T1Hits++;
        }
    }
}
