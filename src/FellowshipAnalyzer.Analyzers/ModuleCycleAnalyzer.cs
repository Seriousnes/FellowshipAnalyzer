using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// FA0013: Module constructor dependency cycle. Constructor injection between modules forms a
/// DAG; cycles break topological resolution. Escape ladder, in order of preference:
/// (a) event-based decoupling, (b) <c>Lazy&lt;TOther&gt;</c> in the ctor (the generator threads
/// it through without participating in cycle detection), (c) result-level composition, or
/// (d) the last-resort <c>Owner.GetModule&lt;T&gt;()</c> service-locator lookup.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModuleCycleAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0013";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Circular module constructor dependency",
        messageFormat: "Circular module ctor dependency: {0}. Escape options (in order): event-based decoupling, Lazy<T> ctor injection, result-level composition, or Owner.GetModule<T>() as a last resort.",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,        
        description: "Module constructors form a DAG that the parser resolves topologically. Cycles can be broken by Lazy<TOther> ctor injection (which does not participate in cycle detection), event fabrication, or by composing the cycle at the typed-result layer instead of at the module layer.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var modules = CollectModules(context.Compilation, context.CancellationToken);
        if (modules.Count == 0) return;

        var deps = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var module in modules)
        {
            deps[module] = [];
            foreach (var ctor in module.InstanceConstructors)
            {
                foreach (var param in ctor.Parameters)
                {
                    if (param.Type is not INamedTypeSymbol paramType) continue;

                    if (paramType.Name == "Lazy" && paramType.TypeArguments.Length == 1) continue;
                    if (modules.Contains(paramType))
                        deps[module].Add(paramType);
                }
            }
        }

        var reportedCycles = new HashSet<string>();
        foreach (var module in modules)
        {
            var path = new List<INamedTypeSymbol>();
            var onPath = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            DetectCycles(module, deps, path, onPath, visited, context, reportedCycles);
        }
    }

    private static void DetectCycles(
        INamedTypeSymbol node,
        Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> deps,
        List<INamedTypeSymbol> path,
        HashSet<INamedTypeSymbol> onPath,
        HashSet<INamedTypeSymbol> visited,
        CompilationAnalysisContext context,
        HashSet<string> reportedCycles)
    {
        if (visited.Contains(node)) return;
        path.Add(node);
        onPath.Add(node);

        if (deps.TryGetValue(node, out var edges))
        {
            foreach (var next in edges)
            {
                if (onPath.Contains(next))
                {
                    var cycleStart = path.IndexOf(next);
                    var cycleNodes = path.Skip(cycleStart).ToList();
                    cycleNodes.Add(next);
                    var key = BuildCycleKey(cycleNodes);
                    if (reportedCycles.Add(key))
                    {
                        var message = string.Join(" → ", cycleNodes.Select(n => n.Name));
                        var loc = node.Locations.Length > 0 ? node.Locations[0] : Location.None;
                        context.ReportDiagnostic(Diagnostic.Create(Rule, loc, message));
                    }
                }
                else
                {
                    DetectCycles(next, deps, path, onPath, visited, context, reportedCycles);
                }
            }
        }

        onPath.Remove(node);
        visited.Add(node);
        path.RemoveAt(path.Count - 1);
    }

    private static string BuildCycleKey(List<INamedTypeSymbol> cycle)
    {
        var names = cycle.Take(cycle.Count - 1).Select(n => n.ToDisplayString()).ToList();
        var minIndex = 0;
        for (var i = 1; i < names.Count; i++)
            if (string.CompareOrdinal(names[i], names[minIndex]) < 0) minIndex = i;

        var rotated = names.Skip(minIndex).Concat(names.Take(minIndex)).ToList();
        var sb = new StringBuilder();
        foreach (var n in rotated)
        {
            sb.Append(n).Append('|');
        }
        return sb.ToString();
    }

    private static HashSet<INamedTypeSymbol> CollectModules(Compilation compilation, CancellationToken ct)
    {
        var set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();
            if (type.IsAbstract) continue;
            if (InheritsFromByName(type, "Module")) set.Add(type);
        }
        return set;
    }

    private static bool InheritsFromByName(INamedTypeSymbol type, string baseTypeName)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name == baseTypeName) return true;
            current = current.BaseType;
        }
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetAllNestedTypes(type)) yield return nested;
        }
        foreach (var childNs in ns.GetNamespaceMembers())
        foreach (var type in GetAllNamedTypes(childNs))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deepNested in GetAllNestedTypes(nested)) yield return deepNested;
        }
    }
}
