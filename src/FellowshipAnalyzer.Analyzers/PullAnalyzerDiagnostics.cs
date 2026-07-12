using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// Guardrails for the pull-lifetime <c>Analyzer</c> surface, all role-based off the
/// <c>[AddState]</c> / <c>[AddAnalyzer]</c> declarations on parser classes:
/// <list type="bullet">
/// <item>FA0014 — an Analyzer constructor (or <c>Lazy&lt;&gt;</c>) depends on another Analyzer.</item>
/// <item>FA0015 — an Analyzer is missing <c>[ForPull]</c> or declares no targets.</item>
/// <item>FA0016 — two Analyzers producing the same result have overlapping <c>[ForPull]</c> filters.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PullAnalyzerDiagnostics : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DependsOnAnalyzer = new(
        id: "FA0014",
        title: "Analyzer must depend on State only",
        messageFormat: "Analyzer '{0}' depends on Analyzer '{1}'; an Analyzer may depend on State modules only",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Analyzers are constructed fresh per pull and read State for point-in-time snapshots. Depending on another pull-lifetime Analyzer breaks per-pull instantiation. Depend on a State module instead.");

    private static readonly DiagnosticDescriptor MissingForPull = new(
        id: "FA0015",
        title: "Analyzer must declare [ForPull] targets",
        messageFormat: "Analyzer '{0}' must declare [ForPull] with at least one PullKind target",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every Analyzer must declare which pull shapes it runs on via [ForPull(PullKind, Boss = …)] with a non-zero target set.");

    private static readonly DiagnosticDescriptor OverlappingForPull = new(
        id: "FA0016",
        title: "Analyzers sharing a surface must have disjoint [ForPull] filters",
        messageFormat: "Analyzers '{0}' and '{1}' share the surface '{2}' and their [ForPull] filters overlap on a realizable pull",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Two Analyzers sharing one surface type (the topmost ancestor directly below Analyzer) feed one cross-pull stream. Their [ForPull] match sets must not overlap on any realizable pull (single/multi × boss/non-boss), or a pull would retain two analyzers on one surface.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DependsOnAnalyzer, MissingForPull, OverlappingForPull];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var parsers = new List<(INamedTypeSymbol Parser, List<AnalyzerModel> Analyzers)>();
        var allAnalyzers = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var type in GetAllNamedTypes(context.Compilation.Assembly.GlobalNamespace))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var analyzerTypes = CollectDeclaredAnalyzers(type);
            if (analyzerTypes.Count == 0) continue;

            var models = analyzerTypes.Select(BuildModel).ToList();
            parsers.Add((type, models));
            foreach (var t in analyzerTypes) allAnalyzers.Add(t);
        }

        if (allAnalyzers.Count == 0) return;

        var reportedFa14 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var reportedFa15 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var (_, models) in parsers)
        {
            foreach (var model in models)
            {
                if (reportedFa14.Add(model.Type)) CheckDependencies(context, model, allAnalyzers);
                if (reportedFa15.Add(model.Type)) CheckForPull(context, model);
            }

            CheckOverlaps(context, models);
        }
    }

    private static void CheckDependencies(CompilationAnalysisContext context, AnalyzerModel model, HashSet<INamedTypeSymbol> allAnalyzers)
    {
        foreach (var ctor in model.Type.InstanceConstructors)
        {
            foreach (var param in ctor.Parameters)
            {
                if (param.Type is not INamedTypeSymbol paramType) continue;

                var dep = paramType;
                if (paramType.Name == "Lazy" && paramType.TypeArguments.Length == 1
                    && paramType.TypeArguments[0] is INamedTypeSymbol inner)
                    dep = inner;

                if (allAnalyzers.Contains(dep))
                {
                    var location = param.Locations.Length > 0 ? param.Locations[0] : model.Type.Locations[0];
                    context.ReportDiagnostic(Diagnostic.Create(DependsOnAnalyzer, location, model.Type.Name, dep.Name));
                }
            }
        }
    }

    private static void CheckForPull(CompilationAnalysisContext context, AnalyzerModel model)
    {
        if (!model.HasForPull || model.Targets == 0)
        {
            var location = model.Type.Locations.Length > 0 ? model.Type.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(MissingForPull, location, model.Type.Name));
        }
    }

    private static void CheckOverlaps(CompilationAnalysisContext context, List<AnalyzerModel> models)
    {
        for (var i = 0; i < models.Count; i++)
        {
            var a = models[i];

            for (var j = i + 1; j < models.Count; j++)
            {
                var b = models[j];
                if (!SymbolEqualityComparer.Default.Equals(a.SurfaceType, b.SurfaceType)) continue;
                if (!FiltersOverlap(a, b)) continue;

                var location = b.Type.Locations.Length > 0 ? b.Type.Locations[0] : Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    OverlappingForPull, location, a.Type.Name, b.Type.Name, a.SurfaceType.Name));
            }
        }
    }

    /// <summary>True when some realizable pull (single/multi × boss/non-boss) matches both filters.</summary>
    private static bool FiltersOverlap(AnalyzerModel a, AnalyzerModel b)
    {
        foreach (var targetBit in new[] { 1, 2 })
        foreach (var isBoss in new[] { true, false })
        {
            if (Matches(a, targetBit, isBoss) && Matches(b, targetBit, isBoss))
                return true;
        }
        return false;
    }

    private static bool Matches(AnalyzerModel m, int targetBit, bool isBoss)
    {
        if ((m.Targets & targetBit) == 0) return false;
        return m.Boss switch
        {
            1 => isBoss,
            2 => !isBoss,
            _ => true,
        };
    }

    /// <summary>
    /// The analyzers declared directly on <paramref name="parser"/> via <c>[AddAnalyzer]</c>.
    /// FA0016's overlap grouping is per declaring type, which assumes analyzers are declared on leaf
    /// parsers (the generator merges base + own at runtime); base-declared analyzers would need this
    /// to walk the base chain the same way before they could overlap with a derived parser's own.
    /// </summary>
    private static List<INamedTypeSymbol> CollectDeclaredAnalyzers(INamedTypeSymbol parser)
    {
        var result = new List<INamedTypeSymbol>();
        var current = parser;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var attr in current.GetAttributes())
            {
                var ac = attr.AttributeClass;
                if (ac == null || ac.Name != "AddAnalyzerAttribute") continue;
                if (!ac.IsGenericType || ac.TypeArguments.Length == 0) continue;
                if (ac.TypeArguments[0] is INamedTypeSymbol arg) result.Add(arg);
            }
            current = current.BaseType;
        }
        return result;
    }

    private static AnalyzerModel BuildModel(INamedTypeSymbol analyzer)
    {
        var hasForPull = false;
        var targets = 0;
        var boss = 0;
        foreach (var attr in analyzer.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "ForPullAttribute") continue;
            hasForPull = true;
            if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is int t)
                targets = t;
            foreach (var na in attr.NamedArguments)
                if (na.Key == "Boss" && na.Value.Value is int b) boss = b;
            break;
        }

        var surfaceType = analyzer;
        while (surfaceType.BaseType is { } baseType
            && baseType.SpecialType != SpecialType.System_Object
            && baseType.Name != "Analyzer")
        {
            surfaceType = baseType;
        }

        return new AnalyzerModel(analyzer, hasForPull, targets, boss, surfaceType);
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

    private sealed class AnalyzerModel(
        INamedTypeSymbol type, bool hasForPull, int targets, int boss, INamedTypeSymbol surfaceType)
    {
        public INamedTypeSymbol Type { get; } = type;
        public bool HasForPull { get; } = hasForPull;
        public int Targets { get; } = targets;
        public int Boss { get; } = boss;
        public INamedTypeSymbol SurfaceType { get; } = surfaceType;
    }
}
