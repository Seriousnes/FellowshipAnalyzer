using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

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

    // -------------------------------------------------------------------------
    // Basic GCD fabrication
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // CastLinkNormalizer integration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ActivationCast_WithNoDuplicate_ShouldTriggerGcd()
    {
        // Activation-only cast (instant) with no matching non-activation cast.
        // CastLinkNormalizer should keep it; GlobalCooldown should create a GCD.
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Ability = new Ability { Guid = TestSpellId, Name = "Test Spell" },
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
        // When both activation and non-activation CastEvents exist at the same
        // timestamp, the CastLinkNormalizer drops the activation event.
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Ability = new Ability { Guid = TestSpellId, Name = "Test Spell" },
            },
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = false,
                Ability = new Ability { Guid = TestSpellId, Name = "Test Spell" },
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
        // A fake+activation cast event should be removed by the normalizer.
        // The real begincast (if present) is kept instead.
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Fake = true,
                Ability = new Ability { Guid = TestSpellId, Name = "Test Spell" },
            },
            new BeginCastEvent
            {
                Timestamp = 1000,
                SourceId = PlayerId,
                Ability = new Ability { Guid = TestSpellId, Name = "Test Spell" },
            },
        };

        var (parser, _) = await RunWithGcd(events, includeNormalizer: true);

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Empty(casts); // The fake cast is dropped; only the BeginCastEvent remains
    }

    // -------------------------------------------------------------------------
    // Timestamp 1623565 scenario (activation cast + interrupt at same time)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ActivationCast_WithInterrupt_AtSameTimestamp_ShouldTriggerGcd()
    {
        // Matches the real data: activation cast for Brain Freeze followed by
        // an interrupt event at the same timestamp, with no non-activation cast.
        var events = new List<Event>
        {
            new CastEvent
            {
                Timestamp = 1623565,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Activation = true,
                Ability = new Ability { Guid = BrainFreezeSpellId, Name = "Brain Freeze" },
            },
            new RemoveDebuffEvent
            {
                Timestamp = 1623565,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { Guid = 1000001, Name = "Mounted" },
            },
            new InterruptEvent
            {
                Timestamp = 1623565,
                SourceId = PlayerId,
                TargetId = EnemyId,
                Ability = new Ability { Guid = BrainFreezeSpellId, Name = "Brain Freeze" },
                ExtraAbility = new Ability { Guid = 734, Name = "Shattering Barrier" },
            },
        };

        var (parser, _) = await RunWithGcd(
            events,
            includeNormalizer: true,
            configureAbilities: a => a.AddSpell(new SpellbookAbility
            {
                PrimarySpell = new Spell(BrainFreezeSpellId, "Brain Freeze"),
                Category = SpellCategory.Utility,
                Gcd = Abilities.StandardGcd,
            }));

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Single(casts);
        Assert.NotNull(casts[0].GlobalCooldown);
    }

    // -------------------------------------------------------------------------
    // GCD overlap detection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OverlappingCasts_ShouldStillCreateGcdEvents()
    {
        // Two casts within the same GCD window. Both should get GCD events.
        var events = new List<Event>
        {
            CreateCast(1000, TestSpellId),
            CreateCast(1200, TestSpellId), // 200ms later, within the 1500ms GCD
        };

        var (parser, _) = await RunWithGcd(events);

        var casts = parser.Events.OfType<CastEvent>().ToList();
        Assert.Equal(2, casts.Count);
        Assert.NotNull(casts[0].GlobalCooldown);
        Assert.NotNull(casts[1].GlobalCooldown);
    }

    // -------------------------------------------------------------------------
    // Static GCD (no haste scaling)
    // -------------------------------------------------------------------------

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
                PrimarySpell = new Spell(staticSpellId, "Static Spell"),
                Category = SpellCategory.Rotational,
                Gcd = new GcdInfo { Static = 1000.0 },
            }));

        var cast = parser.Events.OfType<CastEvent>().First();
        Assert.NotNull(cast.GlobalCooldown);
        Assert.Equal(1000, cast.GlobalCooldown!.Duration);
    }

    // =====================================================================
    // Test infrastructure
    // =====================================================================

    private static async Task<(TestCombatLogParser parser, GlobalCooldown gcd)> RunWithGcd(
        List<Event>? events = null,
        bool includeNormalizer = false,
        Action<TestAbilities>? configureAbilities = null)
    {
        var abilities = new TestAbilities();
        // Register a default test spell with standard GCD
        abilities.AddSpell(new SpellbookAbility
        {
            PrimarySpell = new Spell(TestSpellId, "Test Spell"),
            Category = SpellCategory.Rotational,
            Gcd = Abilities.StandardGcd,
        });
        configureAbilities?.Invoke(abilities);

        var gcd = new GlobalCooldown();
        var haste = new Haste();
        var debugAnnotations = new DebugAnnotations();

        Module[] modules = [abilities, haste, debugAnnotations, gcd];

        IEventNormalizer[]? normalizers = includeNormalizer
            ? [new CastLinkNormalizer(abilities)]
            : null;

        var parser = CreateCombatLogParser(modules, normalizers);
        await parser.Analyze(events ?? [], PlayerId, fightStartTime: 0);

        return (parser, gcd);
    }

    private static TestCombatLogParser CreateCombatLogParser(
        Module[] modules,
        IEventNormalizer[]? normalizers = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        foreach (var m in modules)
            provider.GetService(m.GetType()).Returns(m);
        if (normalizers is not null)
            foreach (var n in normalizers)
                provider.GetService(n.GetType()).Returns(n);
        return new TestCombatLogParser(emitter, provider, modules,
            normalizers?.Select(n => n.GetType()).ToArray() ?? []);
    }

    internal sealed class TestCombatLogParser(
        EventEmitter emitter,
        IServiceProvider provider,
        Module[] modules,
        Type[] normalizerTypes)
        : CombatLogParser(emitter, provider)
    {
        public override string HeroId => "test";
        protected override Type[] GetModuleTypes() => [.. modules.Select(m => m.GetType())];
        protected override Type[] GetNormalizerTypes() => normalizerTypes;
    }

    internal class TestAbilities : Abilities
    {
        private readonly List<SpellbookAbility> _spells = [];
        public void AddSpell(SpellbookAbility spell) => _spells.Add(spell);
        public override IEnumerable<SpellbookAbility> Spellbook() => _spells;
    }

    // -------------------------------------------------------------------------
    // Event factories
    // -------------------------------------------------------------------------

    private static CastEvent CreateCast(int timestamp, int spellId, bool activation = false) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
        Activation = activation,
    };

    private static BeginChannelEvent CreateBeginChannel(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
    };
}
