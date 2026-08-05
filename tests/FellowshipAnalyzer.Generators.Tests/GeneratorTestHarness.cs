using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FellowshipAnalyzer.Generators.Tests;

internal static class GeneratorTestHarness
{
    private const string Preamble = """
        namespace FellowshipAnalyzer.Core.Analysis
        {
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class OnAttribute<TEvent> : System.Attribute
                where TEvent : FellowshipAnalyzer.Core.Events.Event
            {
                public int By { get; set; }
                public int To { get; set; }
                public string? Spell { get; set; }
                public string[]? Spells { get; set; }
                public string? ExtraSpell { get; set; }
                public string[]? ExtraSpells { get; set; }
            }

            public class Module
            {
                public bool Active { get; protected set; } = true;
                public int Priority { get; set; }
                public CombatLogParser Owner { get; set; } = null!;
            }

            public class Analyzer : Module
            {
                internal int NumExecutions { get; set; }
                protected virtual void RegisterAttributeSubscriptions() { }
            }

            public class CombatLogParser
            {
                public EventEmitter EventEmitter { get; } = new EventEmitter();
                public bool ByPlayer(FellowshipAnalyzer.Core.Events.Event e, object? p) => true;
                public bool ByPlayerPet(FellowshipAnalyzer.Core.Events.Event e) => true;
                public bool ToPlayer(FellowshipAnalyzer.Core.Events.Event e, object? p) => true;
                public bool ToPlayerPet(FellowshipAnalyzer.Core.Events.Event e) => true;
            }

            public class EventEmitter
            {
                public void Subscribe(Analyzer m,
                    System.Func<FellowshipAnalyzer.Core.Events.Event, bool> filter,
                    System.Action<FellowshipAnalyzer.Core.Events.Event> a) { }
                public void Subscribe(Analyzer m,
                    System.Func<FellowshipAnalyzer.Core.Events.Event, bool> filter,
                    System.Func<FellowshipAnalyzer.Core.Events.Event, System.Threading.Tasks.Task> a) { }
            }
        }

        namespace FellowshipAnalyzer.Core.Events
        {
            public class Event { public long Timestamp; }
            public interface IAbilityEvent { }
            public interface IExtraAbilityEvent { }
            public interface IHasSourceEvent { }
            public interface IHasTargetEvent { }
            public class Ability { public int Id; }
            public class BaseCastEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent { public Ability Ability = null!; }
            public class CastEvent : BaseCastEvent { }
            public class FreeCastEvent : CastEvent { }
            public class HealEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent { public Ability Ability = null!; }
            public class DamageEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent { }
            public class DeathEvent : Event, IAbilityEvent, IHasTargetEvent { }
            public class ApplyBuffEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent { }
            public class AbsorbedEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent { }
        }

        namespace OneOf
        {
            public class OneOf<T0>
            {
                public static OneOf<T0> FromT0(T0 v) => new();
            }
            public class OneOf<T0, T1>
            {
                public static OneOf<T0, T1> FromT0(T0 v) => new();
                public static OneOf<T0, T1> FromT1(T1 v) => new();
            }
            public class OneOf<T0, T1, T2>
            {
                public static OneOf<T0, T1, T2> FromT0(T0 v) => new();
                public static OneOf<T0, T1, T2> FromT1(T1 v) => new();
                public static OneOf<T0, T1, T2> FromT2(T2 v) => new();
            }
        }
        """;

    public static GeneratorRunResult Run(string userSource)
    {
        var preambleTree = CSharpSyntaxTree.ParseText(Preamble);
        var userTree = CSharpSyntaxTree.ParseText(userSource);

        var references = new[]
        {
            typeof(object).Assembly,
            typeof(System.Threading.Tasks.Task).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(Attribute).Assembly,
        };

        var trustedAssembliesPaths =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator);

        var refList = trustedAssembliesPaths
            .Where(p => File.Exists(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests.Compilation",
            syntaxTrees: [preambleTree, userTree],
            references: refList,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(new ModuleGenerator().AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var driverResult = driver.GetRunResult();

        var generatedTexts = driverResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToImmutableArray();

        var compilationDiagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToImmutableArray();

        return new GeneratorRunResult(generatedTexts, driverDiagnostics, compilationDiagnostics);
    }
}

internal sealed record GeneratorRunResult(
    ImmutableArray<string> GeneratedSources,
    ImmutableArray<Diagnostic> DriverDiagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics)
{
    public string ConcatenatedGenerated => string.Join("\n\n", GeneratedSources);
}
