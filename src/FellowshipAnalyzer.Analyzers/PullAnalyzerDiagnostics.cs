using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// Guardrails for analyzer and module registration. <c>[ForPull]</c> is the sole lifetime
/// determinant, so every rule keys off its presence rather than off which attribute registered
/// the type:
/// <list type="bullet">
/// <item>FA0014 — a registered type or normalizer depends on a <c>[ForPull]</c> analyzer.</item>
/// <item>FA0015 — a <c>[ForPull]</c> declares no <c>PullKind</c> target.</item>
/// <item>FA0016 — two <c>[ForPull]</c> analyzers sharing a surface have overlapping filters.</item>
/// <item>FA0017 — a <c>[ForPull]</c> analyzer implements more than one surface marker interface.</item>
/// <item>FA0019 — <c>[AddModule]</c> / <c>[AddState]</c> registers a type deriving from <c>Analyzer</c>.</item>
/// <item>FA0020 — <c>[ForPull]</c> sits on a type that does not derive from <c>Analyzer</c>.</item>
/// <item>FA0021 — <c>[ForPull]</c> sits on an abstract <c>Analyzer</c>.</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PullAnalyzerDiagnostics : DiagnosticAnalyzer
{
    private const string AnalyzerBaseName = "Analyzer";
    private const string ForPullAttributeName = "ForPullAttribute";
    private const string DependencyAttributeName = "DependencyAttribute";
    private const string AddModuleAttributeName = "AddModuleAttribute";
    private const string AddStateAttributeName = "AddStateAttribute";
    private const string AddAnalyzerAttributeName = "AddAnalyzerAttribute";
    private const string AddNormalizerAttributeName = "AddNormalizerAttribute";
    private const string SurfaceInterfaceName = "IAnalyzerSurface";

    private static readonly DiagnosticDescriptor DependsOnPullAnalyzer = new(
        id: "FA0014",
        title: "Dependency on a pull-lifetime analyzer",
        messageFormat: "'{0}' depends on '{1}', which declares [ForPull]",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [ForPull] analyzer is constructed fresh for every matching pull, directly into the parser's per-pull cache, and no dependency-resolution path reads that cache. Depending on one throws at runtime. Depend on a parse-lifetime module or analyzer instead.");

    private static readonly DiagnosticDescriptor ForPullMissingTargets = new(
        id: "FA0015",
        title: "[ForPull] must declare at least one PullKind target",
        messageFormat: "[ForPull] on '{0}' must declare at least one PullKind target",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[ForPull(PullKind, Boss = …)] declares which pull shapes an analyzer runs on. An empty target set matches no pull, so the analyzer would never be constructed.");

    private static readonly DiagnosticDescriptor OverlappingForPull = new(
        id: "FA0016",
        title: "Analyzers sharing a surface must have disjoint [ForPull] filters",
        messageFormat: "Analyzers '{0}' and '{1}' share the surface '{2}' and their [ForPull] filters overlap on a realizable pull",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Two Analyzers sharing one surface (a surface marker interface, or else the topmost ancestor directly below Analyzer) feed one cross-pull stream. Their [ForPull] match sets must not overlap on any realizable pull (single/multi × boss/non-boss), or a pull would retain two analyzers on one surface.");

    private static readonly DiagnosticDescriptor MultipleSurfaceInterfaces = new(
        id: "FA0017",
        title: "Analyzer must implement at most one surface interface",
        messageFormat: "Analyzer '{0}' implements multiple surface interfaces ({1}); an analyzer's surface must be unambiguous",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An analyzer's surface (the type its cross-pull stream and per-pull accessor are keyed on) must be unique. Implementing more than one IAnalyzerSurface marker interface leaves the surface ambiguous.");

    private static readonly DiagnosticDescriptor ModuleRegistrationOfAnalyzer = new(
        id: "FA0019",
        title: "[AddModule] and [AddState] register non-subscribers only",
        messageFormat: "'{0}' derives from Analyzer, so it is registered with [AddAnalyzer<{0}>]",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[AddAnalyzer] registers every event subscriber; [AddModule] and [AddState] register everything that is not one. A type deriving from Analyzer subscribes to events and belongs under [AddAnalyzer], whichever lifetime its [ForPull] gives it.");

    private static readonly DiagnosticDescriptor ForPullRequiresAnalyzer = new(
        id: "FA0020",
        title: "[ForPull] requires a type deriving from Analyzer",
        messageFormat: "[ForPull] on '{0}' requires a type deriving from Analyzer",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[ForPull] makes a type pull-lifetime, which only the Analyzer surface supports: the parser assigns Analyzer.Pull, retains the instance on the pull read surfaces, and keys it by its analyzer surface type.");

    private static readonly DiagnosticDescriptor ForPullOnAbstractAnalyzer = new(
        id: "FA0021",
        title: "[ForPull] may not sit on an abstract Analyzer",
        messageFormat: "[ForPull] on abstract Analyzer '{0}' is dead metadata; declare it on each concrete subclass",
        category: "Analysis",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ForPullAttribute is Inherited = false, and both the generator and these diagnostics read directly-applied attributes only. On an abstract base the attribute reads as though it applied to every subclass while gating none of them. An abstract pull-analyzer base declares the shape; each concrete subclass declares its own [ForPull].");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DependsOnPullAnalyzer,
        ForPullMissingTargets,
        OverlappingForPull,
        MultipleSurfaceInterfaces,
        ModuleRegistrationOfAnalyzer,
        ForPullRequiresAnalyzer,
        ForPullOnAbstractAnalyzer,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var parsers = new List<List<Registration>>();

        foreach (var type in GetAllNamedTypes(context.Compilation.Assembly.GlobalNamespace))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            CheckForPullPlacement(context, type);

            var registrations = CollectRegistrations(type);
            if (registrations.Count > 0) parsers.Add(registrations);
        }

        var reportedFa14 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var reportedFa15 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var reportedFa17 = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var registrations in parsers)
        {
            var pullAnalyzers = new List<AnalyzerModel>();

            foreach (var registration in registrations)
            {
                if (reportedFa14.Add(registration.Type)) CheckDependencies(context, registration.Type);

                CheckModuleRegistration(context, registration);

                if (!HasForPull(registration.Type)) continue;

                var model = BuildModel(registration.Type);
                pullAnalyzers.Add(model);

                if (reportedFa15.Add(model.Type)) CheckTargets(context, model);
                if (reportedFa17.Add(model.Type)) CheckSurfaceInterfaces(context, model.Type);
            }

            CheckOverlaps(context, pullAnalyzers);
        }
    }

    /// <summary>
    /// FA0020 and FA0021: where <c>[ForPull]</c> may sit. Both are properties of the type alone, so
    /// they are checked over every type in the compilation that declares the attribute, registered or
    /// not, and report in the assembly that declares the type.
    /// </summary>
    private static void CheckForPullPlacement(CompilationAnalysisContext context, INamedTypeSymbol type)
    {
        if (FindForPull(type) is not { } forPull) return;

        var location = AttributeLocation(forPull, type);

        if (!DerivesFromAnalyzer(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(ForPullRequiresAnalyzer, location, type.Name));
            return;
        }

        if (type.IsAbstract)
            context.ReportDiagnostic(Diagnostic.Create(ForPullOnAbstractAnalyzer, location, type.Name));
    }

    /// <summary>
    /// FA0019: <c>[AddModule&lt;T&gt;]</c> and <c>[AddState&lt;T&gt;]</c> take a non-subscriber. Reports at
    /// the attribute application on the parser, so it is skipped for a base-chain attribute read from
    /// metadata: that has no application syntax in this compilation and was already checked when its
    /// own assembly built.
    /// </summary>
    private static void CheckModuleRegistration(CompilationAnalysisContext context, Registration registration)
    {
        if (!registration.IsModuleAttribute) return;
        if (!DerivesFromAnalyzer(registration.Type)) return;
        if (registration.Attribute.ApplicationSyntaxReference is not { } syntaxRef) return;

        context.ReportDiagnostic(Diagnostic.Create(
            ModuleRegistrationOfAnalyzer,
            Location.Create(syntaxRef.SyntaxTree, syntaxRef.Span),
            registration.Type.Name));
    }

    /// <summary>
    /// FA0014: nothing the parser resolves may depend on a <c>[ForPull]</c> type. <c>BeginPull</c>
    /// constructs pull instances straight into its per-pull cache, which no resolution path reads, so
    /// a registered type or a normalizer naming one throws "No registered module is assignable" at
    /// runtime. Covers constructor parameters, <c>Lazy&lt;T&gt;</c> parameters, and
    /// <c>[Dependency&lt;T&gt;]</c> — the last reports at the attribute, because a
    /// <c>[Dependency&lt;T&gt;]</c>-only type's constructor exists solely in generated code, which
    /// <see cref="GeneratedCodeAnalysisFlags.None"/> suppresses diagnostics in.
    /// </summary>
    private static void CheckDependencies(CompilationAnalysisContext context, INamedTypeSymbol type)
    {
        foreach (var ctor in type.InstanceConstructors)
        {
            foreach (var param in ctor.Parameters)
            {
                if (param.Type is not INamedTypeSymbol paramType) continue;
                if (!HasForPull(Unwrap(paramType))) continue;

                var location = param.Locations.Length > 0 ? param.Locations[0] : type.Locations[0];
                context.ReportDiagnostic(Diagnostic.Create(
                    DependsOnPullAnalyzer, location, type.Name, Unwrap(paramType).Name));
            }
        }

        foreach (var attr in type.GetAttributes())
        {
            var ac = attr.AttributeClass;
            if (ac is not { IsGenericType: true } || ac.Name != DependencyAttributeName) continue;
            if (ac.TypeArguments.Length != 1 || ac.TypeArguments[0] is not INamedTypeSymbol dep) continue;
            if (!HasForPull(dep)) continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DependsOnPullAnalyzer, AttributeLocation(attr, type), type.Name, dep.Name));
        }
    }

    /// <summary>The dependency a parameter names, seeing through a <c>Lazy&lt;T&gt;</c> wrapper.</summary>
    private static INamedTypeSymbol Unwrap(INamedTypeSymbol paramType) =>
        paramType.Name == "Lazy" && paramType.TypeArguments.Length == 1
            && paramType.TypeArguments[0] is INamedTypeSymbol inner
                ? inner
                : paramType;

    private static void CheckTargets(CompilationAnalysisContext context, AnalyzerModel model)
    {
        if (model.Targets != 0) return;

        context.ReportDiagnostic(Diagnostic.Create(ForPullMissingTargets, model.Location, model.Type.Name));
    }

    private static void CheckSurfaceInterfaces(CompilationAnalysisContext context, INamedTypeSymbol analyzer)
    {
        var surfaces = GetSurfaceInterfaces(analyzer);
        if (surfaces.Count <= 1) return;

        var names = new List<string>(surfaces.Count);
        foreach (var s in surfaces) names.Add(s.Name);

        var location = analyzer.Locations.Length > 0 ? analyzer.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(MultipleSurfaceInterfaces, location, analyzer.Name, string.Join(", ", names)));
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
    /// Every type <paramref name="parser"/> registers — from all three registration attributes plus
    /// <c>[AddNormalizer]</c>, which FA0014 covers alongside them — walking the base chain so a hero
    /// parser also sees what <c>CombatLogParser</c> registers. FA0016's overlap grouping is per parser,
    /// which the walk is what makes correct.
    /// </summary>
    private static List<Registration> CollectRegistrations(INamedTypeSymbol parser)
    {
        var result = new List<Registration>();
        var current = parser;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var attr in current.GetAttributes())
            {
                var ac = attr.AttributeClass;
                if (ac is not { IsGenericType: true } || ac.TypeArguments.Length == 0) continue;

                var isModuleAttribute = ac.Name is AddModuleAttributeName or AddStateAttributeName;
                if (!isModuleAttribute
                    && ac.Name != AddAnalyzerAttributeName
                    && ac.Name != AddNormalizerAttributeName) continue;
                if (ac.TypeArguments[0] is not INamedTypeSymbol arg) continue;

                result.Add(new Registration(arg, attr, isModuleAttribute));
            }
            current = current.BaseType;
        }
        return result;
    }

    private static AnalyzerModel BuildModel(INamedTypeSymbol analyzer)
    {
        var targets = 0;
        var boss = 0;
        var attr = FindForPull(analyzer);
        if (attr is not null)
        {
            if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is int t)
                targets = t;
            foreach (var na in attr.NamedArguments)
                if (na.Key == "Boss" && na.Value.Value is int b) boss = b;
        }

        var surfaces = GetSurfaceInterfaces(analyzer);
        INamedTypeSymbol surfaceType;
        if (surfaces.Count > 0)
        {
            surfaceType = surfaces[0];
        }
        else
        {
            surfaceType = analyzer;
            while (surfaceType.BaseType is { } baseType
                && baseType.SpecialType != SpecialType.System_Object
                && baseType.Name != AnalyzerBaseName)
            {
                surfaceType = baseType;
            }
        }

        return new AnalyzerModel(analyzer, targets, boss, surfaceType, AttributeLocation(attr, analyzer));
    }

    private static AttributeData? FindForPull(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (attr.AttributeClass?.Name == ForPullAttributeName) return attr;
        }
        return null;
    }

    private static bool HasForPull(INamedTypeSymbol type) => FindForPull(type) is not null;

    private static bool DerivesFromAnalyzer(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name == AnalyzerBaseName) return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Where an attribute application sits, falling back to the annotated type when the attribute was
    /// read from metadata rather than source.
    /// </summary>
    private static Location AttributeLocation(AttributeData? attr, INamedTypeSymbol type)
    {
        if (attr?.ApplicationSyntaxReference is { } syntaxRef)
            return Location.Create(syntaxRef.SyntaxTree, syntaxRef.Span);
        return type.Locations.Length > 0 ? type.Locations[0] : Location.None;
    }

    /// <summary>
    /// The surface marker interfaces <paramref name="analyzer"/> implements: interfaces (other than
    /// <c>IAnalyzerSurface</c> itself) that implement <c>IAnalyzerSurface</c>. Mirrors the generator
    /// and runtime surface resolution; at most one is valid (FA0017).
    /// </summary>
    private static List<INamedTypeSymbol> GetSurfaceInterfaces(INamedTypeSymbol analyzer)
    {
        var result = new List<INamedTypeSymbol>();
        foreach (var iface in analyzer.AllInterfaces)
        {
            if (iface.Name == SurfaceInterfaceName) continue;
            foreach (var baseIface in iface.AllInterfaces)
            {
                if (baseIface.Name == SurfaceInterfaceName)
                {
                    result.Add(iface);
                    break;
                }
            }
        }
        return result;
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

    private sealed class Registration(INamedTypeSymbol type, AttributeData attribute, bool isModuleAttribute)
    {
        public INamedTypeSymbol Type { get; } = type;
        public AttributeData Attribute { get; } = attribute;
        public bool IsModuleAttribute { get; } = isModuleAttribute;
    }

    private sealed class AnalyzerModel(
        INamedTypeSymbol type, int targets, int boss, INamedTypeSymbol surfaceType, Location location)
    {
        public INamedTypeSymbol Type { get; } = type;
        public int Targets { get; } = targets;
        public int Boss { get; } = boss;
        public INamedTypeSymbol SurfaceType { get; } = surfaceType;
        public Location Location { get; } = location;
    }
}
