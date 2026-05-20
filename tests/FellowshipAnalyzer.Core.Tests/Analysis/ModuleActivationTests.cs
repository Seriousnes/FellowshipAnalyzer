using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Verifies <see cref="ActiveWhenAttribute{TPredicate}"/> drives the source-generated
/// <c>IsModuleActive</c> override, and that <see cref="BeforeAttribute{TOther}"/> /
/// <see cref="AfterAttribute{TOther}"/> reorder the generated <c>GetModuleTypes</c> output.
/// Tests bypass <see cref="CombatLogParser.Analyze"/> so the base-module DI graph isn't
/// required — we only need to inspect the generator's emission.
/// </summary>
public sealed class ModuleActivationTests
{
    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 0, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public void IsModuleActive_WhenPredicateFalse_ReturnsFalse()
    {
        var parser = CreateGatedParser();
        var inactiveCtx = new ParseContext(PlayerId: 0, Fight: TestFight, ActorNames: new Dictionary<int, string>(), Substitute.For<Combatant>());

        Assert.False(parser.InvokeIsModuleActive(typeof(GatedProbeModule), inactiveCtx));
    }

    [Fact]
    public void IsModuleActive_WhenPredicateTrue_ReturnsTrue()
    {
        var parser = CreateGatedParser();
        var activeCtx = new ParseContext(PlayerId: 7, Fight: TestFight, ActorNames: new Dictionary<int, string>(), Substitute.For<Combatant>());

        Assert.True(parser.InvokeIsModuleActive(typeof(GatedProbeModule), activeCtx));
    }

    [Fact]
    public void IsModuleActive_ForUnGatedModule_ReturnsTrue()
    {
        // Module without [ActiveWhen<>] falls through the generated switch to the base
        // implementation, which always returns true.
        var parser = CreateGatedParser();
        var anyCtx = new ParseContext(PlayerId: 0, Fight: TestFight, ActorNames: new Dictionary<int, string>(), Substitute.For<Combatant>());

        Assert.True(parser.InvokeIsModuleActive(typeof(OrderedModuleA), anyCtx));
    }

    [Fact]
    public void TopologicalSort_HonorsBeforeAndAfter()
    {
        var parser = CreateOrderedParser();
        var moduleTypes = parser.InvokeGetModuleTypes();

        var indexA = Array.IndexOf(moduleTypes, typeof(OrderedModuleA));
        var indexB = Array.IndexOf(moduleTypes, typeof(OrderedModuleB));
        var indexC = Array.IndexOf(moduleTypes, typeof(OrderedModuleC));

        Assert.NotEqual(-1, indexA);
        Assert.NotEqual(-1, indexB);
        Assert.NotEqual(-1, indexC);
        // C declares [Before<A>] and [After<B>], so the final order must satisfy B < C < A.
        Assert.True(indexB < indexC, $"B (idx {indexB}) should come before C (idx {indexC})");
        Assert.True(indexC < indexA, $"C (idx {indexC}) should come before A (idx {indexA})");
    }

    private static GatedTestParser CreateGatedParser()
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        return new GatedTestParser(emitter, provider);
    }

    private static OrderedTestParser CreateOrderedParser()
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        return new OrderedTestParser(emitter, provider);
    }
}

/// <summary>Predicate gating <see cref="GatedProbeModule"/> on a non-zero player id.</summary>
public sealed class NonZeroPlayerId : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => context.PlayerId > 0;
}

[ActiveWhen<NonZeroPlayerId>]
public sealed class GatedProbeModule : Module { }

[AddModule<GatedProbeModule>]
public sealed partial class GatedTestParser : CombatLogParser
{
    public bool InvokeIsModuleActive(Type moduleType, ParseContext context) =>
        IsModuleActive(moduleType, context);
}

public sealed class OrderedModuleA : Module { }
public sealed class OrderedModuleB : Module { }

[Before<OrderedModuleA>]
[After<OrderedModuleB>]
public sealed class OrderedModuleC : Module { }

[AddModule<OrderedModuleA>]
[AddModule<OrderedModuleB>]
[AddModule<OrderedModuleC>]
public sealed partial class OrderedTestParser : CombatLogParser
{
    public Type[] InvokeGetModuleTypes() => GetModuleTypes();
}
