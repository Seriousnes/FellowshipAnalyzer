using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class GlobalCooldownTests
{
    private const int PlayerId = 3;
    private const int EnemyId = 8;
    private const int TestSpellId = 42;
    private const int BrainFreezeSpellId = 1019;

    [Fact]
    public async Task Cast_WithGcdAbility_ShouldCreateGcdEvent()
    {
        var events = new List<Event>
        {
            CreateCast(1000, TestSpellId),
        };

        var (parser, _) = await RunWithGcd(events);

        var cast = parser.Events.OfType<CastEvent>().First();
        Assert.NotNull(cast.GlobalCooldown);
        Assert.Equal(1500, cast.GlobalCooldown!.Duration);
    }

    [Fact]
    public async Task Cast_WithNoGcdAbility_ShouldNotCreateGcdEvent()
    {
        var unknownSpellId = 999;
        var events = new List<Event>
        {
            CreateCast(1000, unknownSpellId),
        };

        var (parser, _) = await RunWithGcd(events);

        var cast = parser.Events.OfType<CastEvent>().First();
        Assert.Null(cast.GlobalCooldown);
    }

    [Fact]
    public async Task BeginChannel_WithGcdAbility_ShouldCreateGcdEvent()
    {
        var events = new List<Event>
        {
            CreateBeginChannel(1000, TestSpellId),
        };

        var (parser, _) = await RunWithGcd(events);

        var channel = parser.Events.OfType<BeginChannelEvent>().First();
        Assert.NotNull(channel.GlobalCooldown);
        Assert.Equal(1500, channel.GlobalCooldown!.Duration);
    }

    [Fact]
    public async Task ActivationCast_WithNoDuplicate_ShouldTriggerGcd()
    {
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Ability = new Ability { FSLID = TestSpellId, Name = "Test Spell" },
            },
        };

        var (parser, _) = await RunWithGcd(events, includeNormalizer: true);

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Single(casts);
        Assert.NotNull(casts[0].GlobalCooldown);
        Assert.Equal(1500, casts[0].GlobalCooldown!.Duration);
    }

    [Fact]
    public async Task ActivationCast_WithDuplicate_ShouldBeDropped()
    {
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Ability = new Ability { FSLID = TestSpellId, Name = "Test Spell" },
            },
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = false,
                Ability = new Ability { FSLID = TestSpellId, Name = "Test Spell" },
            },
        };

        var (parser, _) = await RunWithGcd(events, includeNormalizer: true);

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Single(casts);
        Assert.False(casts[0].Activation);
        Assert.NotNull(casts[0].GlobalCooldown);
    }

    [Fact]
    public async Task FakeActivationCast_ShouldBeDropped()
    {
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Fake = true,
                Ability = new Ability { FSLID = TestSpellId, Name = "Test Spell" },
            },
            new BeginCastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                Ability = new Ability { FSLID = TestSpellId, Name = "Test Spell" },
            },
        };

        var (parser, _) = await RunWithGcd(events, includeNormalizer: true);

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Empty(casts);
    }

    [Fact]
    public async Task ActivationCast_WithInterrupt_AtSameTimestamp_ShouldTriggerGcd()
    {
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1623565,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Ability = new Ability { FSLID = BrainFreezeSpellId, Name = "Brain Freeze" },
            },
            new RemoveDebuffEvent
            {
                Timestamp = 1623565,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { FSLID = 1000001, Name = "Mounted" },
            },
            new InterruptEvent
            {
                Timestamp = 1623565,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Ability = new Ability { FSLID = BrainFreezeSpellId, Name = "Brain Freeze" },
                ExtraAbility = new Ability { FSLID = 734, Name = "Shattering Barrier" },
            },
        };

        var (parser, _) = await RunWithGcd(
            events,
            includeNormalizer: true,
            configureAbilities: a => a.AddSpell(new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = BrainFreezeSpellId, Name = "Brain Freeze" },
                Category = SpellCategory.Utility,
                Gcd = Abilities.StandardGcd,
            }));

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Single(casts);
        Assert.NotNull(casts[0].GlobalCooldown);
    }

    [Fact]
    public async Task OverlappingCasts_ShouldStillCreateGcdEvents()
    {
        var events = new List<Event>
        {
            CreateCast(1000, TestSpellId),
            CreateCast(1200, TestSpellId),
        };

        var (parser, _) = await RunWithGcd(events);

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Equal(2, casts.Count);
        Assert.NotNull(casts[0].GlobalCooldown);
        Assert.NotNull(casts[1].GlobalCooldown);
    }

    [Fact]
    public async Task Cast_WithStaticGcd_ShouldUseStaticDuration()
    {
        var staticSpellId = 50;
        var events = new List<Event>
        {
            CreateCast(1000, staticSpellId),
        };

        var (parser, _) = await RunWithGcd(events, configureAbilities: a =>
            a.AddSpell(new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = staticSpellId, Name = "Static Spell" },
                Category = SpellCategory.Rotational,
                Gcd = new GcdInfo { Static = 1000.0 },
            }));

        var cast = parser.Events.OfType<CastEvent>().First();
        Assert.NotNull(cast.GlobalCooldown);
        Assert.Equal(1000, cast.GlobalCooldown!.Duration);
    }

    private static async Task<(TestCombatLogParser parser, GlobalCooldown gcd)> RunWithGcd(
        List<Event>? events = null,
        bool includeNormalizer = false,
        Action<TestAbilities>? configureAbilities = null)
    {
        Type[] moduleTypes = [typeof(TestAbilities), typeof(Haste), typeof(DebugAnnotations), typeof(GlobalCooldown)];
        Type[] normalizerTypes = includeNormalizer ? [typeof(CastLinkNormalizer)] : [];

        var parser = CreateCombatLogParser(moduleTypes, normalizerTypes, configureAbilities);
        await parser.Analyze(events ?? [], PlayerId, dungeon: new ReportDungeon(0, "", 0, null, 0, 0, null, null, null));

        var gcd = parser.GetModule<GlobalCooldown>()!;
        return (parser, gcd);
    }

    private static TestCombatLogParser CreateCombatLogParser(
        Type[] moduleTypes,
        Type[] normalizerTypes,
        Action<TestAbilities>? configureAbilities)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        provider.GetService(typeof(TestAbilityConfiguration)).Returns(new TestAbilityConfiguration(configureAbilities));
        return new TestCombatLogParser(emitter, provider, moduleTypes, normalizerTypes);
    }

    internal sealed class TestCombatLogParser(
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
            if (type == typeof(TestAbilities))
                return new TestAbilities((TestAbilityConfiguration)Provider.GetService(typeof(TestAbilityConfiguration))!);
            return base.CreateInstance(type);
        }
    }

    internal sealed record TestAbilityConfiguration(Action<TestAbilities>? Configure);

    internal class TestAbilities : Abilities
    {
        private readonly List<SpellbookAbility> _spells = [];

        public TestAbilities(TestAbilityConfiguration configuration)
        {
            AddSpell(new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = TestSpellId, Name = "Test Spell" },
                Category = SpellCategory.Rotational,
                Gcd = StandardGcd,
            });
            configuration.Configure?.Invoke(this);
        }

        public void AddSpell(SpellbookAbility spell) => _spells.Add(spell);
        public override IEnumerable<SpellbookAbility> Spellbook() => _spells;
    }

    private static CastEvent CreateCast(int timestamp, int spellId, bool activation = false) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Activation = activation,
    };

    private static BeginChannelEvent CreateBeginChannel(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };
}
