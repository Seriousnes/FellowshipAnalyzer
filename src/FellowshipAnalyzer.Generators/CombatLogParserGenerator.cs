using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Text;

namespace FellowshipAnalyzer.Generators;

[Generator]
public sealed class CombatLogParserGenerator : IIncrementalGenerator
{
    private const string AddModuleAttributeShortName = "AddModuleAttribute";
    private const string AddNormalizerAttributeShortName = "AddNormalizerAttribute";
    private const string HeroAnalyzerAttributeShortName = "HeroAnalyzerAttribute";
    private const string ActiveWhenAttributeShortName = "ActiveWhenAttribute";
    private const string BeforeAttributeShortName = "BeforeAttribute";
    private const string AfterAttributeShortName = "AfterAttribute";
    private const string CombatLogParserClassName = "CombatLogParser";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var parserInfos = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => GetParserInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        context.RegisterSourceOutput(parserInfos, Execute);
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (!(node is ClassDeclarationSyntax classDecl))
            return false;

        bool isPartial = false;
        foreach (var modifier in classDecl.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                isPartial = true;
                break;
            }
        }
        if (!isPartial) return false;

        return classDecl.AttributeLists.Count > 0;
    }

    private static ParserInfo? GetParserInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
        if (symbol == null)
            return null;

        // Generate for the abstract CombatLogParser base (→ AddCoreAnalysis) or concrete subclasses (→ AddHeroAnalysis).
        bool isCombatLogParserBase = symbol.IsAbstract && symbol.Name == CombatLogParserClassName;
        bool isConcreteParser = !symbol.IsAbstract && InheritsFromCombatLogParser(symbol);

        if (!isCombatLogParserBase && !isConcreteParser)
            return null;

        var ownModules = new List<TypeInfo>();
        var normalizerTypes = new List<TypeInfo>();

        // Collect own [AddModule<>] / [AddNormalizer<>] from syntax (preserves declaration order)
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrSymbol = ctx.SemanticModel.GetSymbolInfo(attr, ct).Symbol as IMethodSymbol;
                if (attrSymbol == null)
                    continue;

                var containingType = attrSymbol.ContainingType;
                if (!containingType.IsGenericType || containingType.TypeArguments.Length == 0)
                    continue;

                var typeArg = containingType.TypeArguments[0] as INamedTypeSymbol;
                if (typeArg == null)
                    continue;

                var ns = GetNamespace(typeArg);

                if (containingType.Name == AddModuleAttributeShortName)
                    ownModules.Add(BuildModuleTypeInfo(typeArg));
                else if (containingType.Name == AddNormalizerAttributeShortName)
                    normalizerTypes.Add(new TypeInfo(typeArg.Name, ns));
            }
        }

        if (ownModules.Count == 0 && normalizerTypes.Count == 0)
            return null;

        var parserNs = GetNamespace(symbol);

        // The abstract CombatLogParser base class emits AddCoreAnalysis (shared registrations).
        if (isCombatLogParserBase)
        {
            return new ParserInfo(
                symbol.Name,
                parserNs,
                [.. ownModules],
                [],
                [.. normalizerTypes],
                [.. normalizerTypes],
                null,
                isAbstractBase: true);
        }

        // Extract [HeroAnalyzer(HeroName.X)] attribute — argument is an enum value.
        // Capture the enum field name so we can emit a strongly typed reference
        // (e.g. global::FellowshipAnalyzer.Core.Analysis.HeroName.Rime) in the keyed DI registration.
        string? heroEnumMember = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != HeroAnalyzerAttributeShortName) continue;
            if (attr.ConstructorArguments.Length != 1) continue;

            var arg = attr.ConstructorArguments[0];
            if (arg.Type is not INamedTypeSymbol enumType || enumType.TypeKind != TypeKind.Enum)
                continue;

            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol field
                    && field.HasConstantValue
                    && Equals(field.ConstantValue, arg.Value))
                {
                    heroEnumMember = field.Name;
                    break;
                }
            }
            break;
        }

        // Walk base type chain to collect inherited base modules (for GetModuleTypes override only)
        var baseModules = new List<TypeInfo>();
        var baseType = symbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            CollectModulesFromSymbol(baseType, baseModules);
            if (baseType.Name == CombatLogParserClassName) break;
            baseType = baseType.BaseType;
        }

        // Walk base type chain to collect inherited base normalizers (for GetNormalizerTypes override only)
        var baseNormalizers = new List<TypeInfo>();
        var bnType = symbol.BaseType;
        while (bnType != null && bnType.SpecialType != SpecialType.System_Object)
        {
            CollectNormalizersFromSymbol(bnType, baseNormalizers);
            if (bnType.Name == CombatLogParserClassName) break;
            bnType = bnType.BaseType;
        }

        return new ParserInfo(
            symbol.Name,
            parserNs,
            [.. ownModules],
            [.. baseModules],
            [.. baseNormalizers, .. normalizerTypes],
            [.. normalizerTypes],
            heroEnumMember,
            isAbstractBase: false);
    }

    private static void CollectModulesFromSymbol(INamedTypeSymbol symbol, List<TypeInfo> modules)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;
            if (attr.AttributeClass.Name != AddModuleAttributeShortName) continue;
            if (!attr.AttributeClass.IsGenericType || attr.AttributeClass.TypeArguments.Length == 0) continue;

            var typeArg = attr.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
            if (typeArg == null) continue;

            modules.Add(BuildModuleTypeInfo(typeArg));
        }
    }

    /// <summary>
    /// Builds a <see cref="TypeInfo"/> for a module type, capturing all module-level metadata
    /// (Abilities inheritance, <c>ToReport()</c> result type, <c>[ActiveWhen&lt;T&gt;]</c>
    /// predicate, and <c>[Before&lt;T&gt;]</c> / <c>[After&lt;T&gt;]</c> ordering constraints).
    /// </summary>
    private static TypeInfo BuildModuleTypeInfo(INamedTypeSymbol moduleType)
    {
        var ns = GetNamespace(moduleType);
        var extendsAbilities = InheritsFromAbilities(moduleType);
        var reportType = TryGetReportType(moduleType);

        string? activePredicate = null;
        var beforeFqns = new List<string>();
        var afterFqns = new List<string>();

        foreach (var attr in moduleType.GetAttributes())
        {
            var ac = attr.AttributeClass;
            if (ac == null || !ac.IsGenericType || ac.TypeArguments.Length == 0) continue;

            var arg = ac.TypeArguments[0] as INamedTypeSymbol;
            if (arg == null) continue;

            var argFqn = FullyQualifiedName(arg);

            if (ac.Name == ActiveWhenAttributeShortName)
                activePredicate = argFqn;
            else if (ac.Name == BeforeAttributeShortName)
                beforeFqns.Add(argFqn);
            else if (ac.Name == AfterAttributeShortName)
                afterFqns.Add(argFqn);
        }

        return new TypeInfo(moduleType.Name, ns, extendsAbilities, reportType, activePredicate,
            beforeFqns.ToImmutableArray(), afterFqns.ToImmutableArray());
    }

    private static string FullyQualifiedName(INamedTypeSymbol t)
    {
        var ns = GetNamespace(t);
        return string.IsNullOrEmpty(ns) ? t.Name : ns + "." + t.Name;
    }

    /// <summary>
    /// A module participates in the typed result projection by exposing a public instance
    /// <c>ToReport()</c> method that takes no parameters. The return type is the report record.
    /// </summary>
    private static string? TryGetReportType(INamedTypeSymbol moduleType)
    {
        foreach (var member in moduleType.GetMembers("ToReport"))
        {
            if (member is not IMethodSymbol method) continue;
            if (method.IsStatic) continue;
            if (method.DeclaredAccessibility != Accessibility.Public) continue;
            if (method.Parameters.Length != 0) continue;
            if (method.ReturnsVoid) continue;
            if (method.ReturnType is not INamedTypeSymbol rt) continue;

            var rtNs = GetNamespace(rt);
            return string.IsNullOrEmpty(rtNs) ? rt.Name : rtNs + "." + rt.Name;
        }
        return null;
    }

    private static void CollectNormalizersFromSymbol(INamedTypeSymbol symbol, List<TypeInfo> normalizers)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;
            if (attr.AttributeClass.Name != AddNormalizerAttributeShortName) continue;
            if (!attr.AttributeClass.IsGenericType || attr.AttributeClass.TypeArguments.Length == 0) continue;

            var typeArg = attr.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
            if (typeArg == null) continue;

            normalizers.Add(new TypeInfo(typeArg.Name, GetNamespace(typeArg)));
        }
    }

    private static string GetNamespace(INamedTypeSymbol symbol) =>
        symbol.ContainingNamespace?.IsGlobalNamespace == false
            ? symbol.ContainingNamespace.ToDisplayString()
            : string.Empty;

    private static bool InheritsFromCombatLogParser(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (current.Name == CombatLogParserClassName)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool InheritsFromAbilities(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (current.Name == "Abilities")
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static string StripSuffix(string name, string suffix)
    {
        if (name.Length > suffix.Length && name.EndsWith(suffix))
            return name.Substring(0, name.Length - suffix.Length);
        return name;
    }

    /// <summary>
    /// Friendly property name for the result record — strips "Analyzer" / "Tracker" / "Module"
    /// suffixes (BasicStComboAnalyzer → BasicStCombo, WinterOrbTracker → WinterOrb).
    /// </summary>
    private static string ReportPropertyName(TypeInfo module)
    {
        var name = module.Name;
        name = StripSuffix(name, "Analyzer");
        name = StripSuffix(name, "Tracker");
        name = StripSuffix(name, "Module");
        return name;
    }

    private static void Execute(SourceProductionContext ctx, ParserInfo info)
    {
        if (info.IsAbstractBase)
            EmitCoreExtension(ctx, info);
        else
            EmitConcreteParser(ctx, info);
    }

    /// <summary>
    /// Kahn's algorithm over the union of base + own modules. Default order = base first
    /// (in declaration order), then own (in declaration order). <c>[Before&lt;X&gt;]</c> creates an
    /// edge module→X; <c>[After&lt;X&gt;]</c> creates an edge X→module. Modules with no incoming
    /// edges drain in original-declaration order so the sort is stable. Cycle fallback: emit
    /// remaining modules in declaration order so the build still succeeds.
    /// </summary>
    private static List<TypeInfo> TopologicalSort(ImmutableArray<TypeInfo> baseModules, ImmutableArray<TypeInfo> ownModules)
    {
        var declarationOrder = new List<TypeInfo>(baseModules.Length + ownModules.Length);
        declarationOrder.AddRange(baseModules);
        declarationOrder.AddRange(ownModules);

        if (declarationOrder.Count == 0) return declarationOrder;

        var byFqn = new Dictionary<string, TypeInfo>(declarationOrder.Count);
        var indexByFqn = new Dictionary<string, int>(declarationOrder.Count);
        for (var i = 0; i < declarationOrder.Count; i++)
        {
            byFqn[declarationOrder[i].FullyQualifiedName] = declarationOrder[i];
            indexByFqn[declarationOrder[i].FullyQualifiedName] = i;
        }

        // Edges: u → v means u must appear before v.
        var edges = new HashSet<(int u, int v)>();
        for (var i = 0; i < declarationOrder.Count; i++)
        {
            var m = declarationOrder[i];
            foreach (var other in m.BeforeModules)
            {
                if (indexByFqn.TryGetValue(other, out var j)) edges.Add((i, j));
            }
            foreach (var other in m.AfterModules)
            {
                if (indexByFqn.TryGetValue(other, out var j)) edges.Add((j, i));
            }
        }

        if (edges.Count == 0) return declarationOrder;

        var inDegree = new int[declarationOrder.Count];
        var outNeighbors = new List<int>[declarationOrder.Count];
        for (var i = 0; i < outNeighbors.Length; i++) outNeighbors[i] = new List<int>();
        foreach (var (u, v) in edges)
        {
            outNeighbors[u].Add(v);
            inDegree[v]++;
        }

        // Min-heap by original index keeps the sort stable / deterministic.
        var ready = new SortedSet<int>();
        for (var i = 0; i < inDegree.Length; i++)
            if (inDegree[i] == 0) ready.Add(i);

        var sorted = new List<TypeInfo>(declarationOrder.Count);
        while (ready.Count > 0)
        {
            var i = ready.Min;
            ready.Remove(i);
            sorted.Add(declarationOrder[i]);
            foreach (var j in outNeighbors[i])
            {
                if (--inDegree[j] == 0) ready.Add(j);
            }
        }

        // Cycle fallback — emit any remaining modules in declaration order.
        if (sorted.Count != declarationOrder.Count)
        {
            var emitted = new HashSet<TypeInfo>(sorted);
            foreach (var m in declarationOrder)
                if (emitted.Add(m)) sorted.Add(m);
        }

        return sorted;
    }

    /// <summary>
    /// Emits an <c>IsModuleActive</c> override that switches on the module Type and invokes
    /// the static <c>IsActive(ParseContext)</c> method of the attribute's predicate. Only emitted
    /// when at least one module in <paramref name="orderedModules"/> declared <c>[ActiveWhen]</c>.
    /// </summary>
    private static void EmitIsModuleActive(StringBuilder sb, List<TypeInfo> orderedModules)
    {
        var gated = orderedModules.FindAll(m => m.ActivePredicateFullyQualified != null);
        if (gated.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("    protected override bool IsModuleActive(global::System.Type moduleType, global::FellowshipAnalyzer.Core.Analysis.ParseContext context)");
        sb.AppendLine("    {");
        foreach (var m in gated)
        {
            sb.AppendLine("        if (moduleType == typeof(global::" + m.FullyQualifiedName + "))");
            sb.AppendLine("            return global::" + m.ActivePredicateFullyQualified + ".IsActive(context);");
        }
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates the partial for a concrete hero parser.
    /// Produces: constructor, computed module properties, GetModuleTypes/GetNormalizerTypes overrides, and DI extension.
    /// </summary>
    private static void EmitConcreteParser(SourceProductionContext ctx, ParserInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using FellowshipAnalyzer.Core.Analysis;");
        sb.AppendLine("using FellowshipAnalyzer.Core.Events;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace " + info.Namespace + ";");
        sb.AppendLine();
        sb.AppendLine("public sealed partial class " + info.ClassName);
        sb.AppendLine("{");

        // Constructor — passes EventEmitter + IServiceProvider to base
        sb.AppendLine("    public " + info.ClassName + "(EventEmitter emitter, IServiceProvider provider) : base(emitter, provider) { }");
        sb.AppendLine();

        // Hero override — emitted when [HeroAnalyzer(HeroName.X)] is present.
        if (info.HeroEnumMember != null)
        {
            sb.AppendLine("    public override global::FellowshipAnalyzer.Core.Analysis.Hero? Hero => global::FellowshipAnalyzer.Core.Analysis.Hero." + info.HeroEnumMember + ";");
            sb.AppendLine();
        }

        // Computed properties for OWN modules only (base module properties are on the base class)
        foreach (var m in info.OwnModules)
        {
            var propName = StripSuffix(m.Name, "Analyzer");
            sb.AppendLine("    public " + m.FullyQualifiedName + "? " + propName + " => GetModule<" + m.FullyQualifiedName + ">();");
        }

        sb.AppendLine();

        // GetModuleTypes — base modules first (higher priority), then own modules, then
        // topologically sorted by any [Before<>] / [After<>] ordering constraints.
        var orderedModules = TopologicalSort(info.BaseModules, info.OwnModules);
        sb.AppendLine("    protected override Type[] GetModuleTypes() =>");
        sb.AppendLine("    [");
        foreach (var m in orderedModules)
            sb.AppendLine("        typeof(" + m.FullyQualifiedName + "),");
        sb.AppendLine("    ];");

        // IsModuleActive override — emitted only when at least one module declares
        // [ActiveWhen<TPredicate>]. Default base implementation returns true for all modules.
        EmitIsModuleActive(sb, orderedModules);

        // GetNormalizerTypes — NormalizerTypes already contains [baseNormalizers, ..ownNormalizers] in order
        if (info.NormalizerTypes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    protected override Type[] GetNormalizerTypes() =>");
            sb.AppendLine("    [");
            foreach (var n in info.NormalizerTypes)
                sb.AppendLine("        typeof(" + n.FullyQualifiedName + "),");
            sb.AppendLine("    ];");
        }

        // Typed-result projection — emit only when at least one module declares ToReport().
        var reportContributors = new List<(TypeInfo Module, string PropertyName)>();
        foreach (var m in info.BaseModules)
        {
            if (m.ReportTypeFullyQualified != null)
                reportContributors.Add((m, ReportPropertyName(m)));
        }
        foreach (var m in info.OwnModules)
        {
            if (m.ReportTypeFullyQualified != null)
                reportContributors.Add((m, ReportPropertyName(m)));
        }
        var parserBaseName = StripSuffix(info.ClassName, "CombatLogParser");
        if (reportContributors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    protected override object? BuildTypedReport()");
            sb.AppendLine("    {");
            sb.Append("        return new ").Append(parserBaseName).Append("AnalysisResult(");
            for (var i = 0; i < reportContributors.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var contrib = reportContributors[i];
                sb.Append("GetModule<global::").Append(contrib.Module.FullyQualifiedName).Append(">()?.ToReport()");
            }
            sb.AppendLine(");");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Emit the typed result record type. Each contributing module gets one nullable property —
        // nullable because a module may be Active=false on a given fight and thus not resolved.
        if (reportContributors.Count > 0)
        {
            sb.Append("public sealed record ").Append(parserBaseName).Append("AnalysisResult(");
            for (var i = 0; i < reportContributors.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var contrib = reportContributors[i];
                sb.Append("global::").Append(contrib.Module.ReportTypeFullyQualified)
                  .Append("? ").Append(contrib.PropertyName);
            }
            sb.AppendLine(");");
            sb.AppendLine();
        }

        // DI extension method — registers only hero-specific services.
        // Shared services (EventEmitter, base modules, base normalizers) come from AddCoreAnalysis().
        sb.AppendLine("public static class " + parserBaseName + "ServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection Add" + parserBaseName + "Analysis(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddScoped<" + info.ClassName + ">();");
        if (info.HeroEnumMember != null)
        {
            sb.AppendLine("        services.AddKeyedScoped<IHeroAnalyzer>(global::FellowshipAnalyzer.Core.Analysis.HeroName." + info.HeroEnumMember + ", (sp, _) => sp.GetRequiredService<" + info.ClassName + ">());");
        }
        foreach (var m in info.OwnModules)
            sb.AppendLine("        services.AddScoped<" + m.FullyQualifiedName + ">();");
        foreach (var n in info.OwnNormalizerTypes)
            sb.AppendLine("        services.AddScoped<" + n.FullyQualifiedName + ">();");
        // Register base Abilities type as an alias so Core normalizers (e.g. CastLinkNormalizer) can inject it.
        foreach (var m in info.OwnModules)
        {
            if (m.ExtendsAbilities)
                sb.AppendLine("        services.AddScoped<global::FellowshipAnalyzer.Core.Analysis.Abilities>(sp => sp.GetRequiredService<" + m.FullyQualifiedName + ">());");
        }
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        ctx.AddSource(info.ClassName + ".g.cs", sb.ToString());
    }

    /// <summary>
    /// Generates the AddCoreAnalysis DI extension from the abstract CombatLogParser base class.
    /// Registers EventEmitter, all base modules, and all base normalizers — shared across every hero.
    /// </summary>
    private static void EmitCoreExtension(SourceProductionContext ctx, ParserInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using FellowshipAnalyzer.Core.Analysis;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace " + info.Namespace + ";");
        sb.AppendLine();

        // Emit computed module accessor properties on the abstract base partial class.
        sb.AppendLine("public abstract partial class " + info.ClassName);
        sb.AppendLine("{");
        foreach (var m in info.OwnModules)
        {
            var propName = StripSuffix(m.Name, "Analyzer");
            sb.AppendLine("    public " + m.FullyQualifiedName + "? " + propName + " => GetModule<" + m.FullyQualifiedName + ">();");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("public static class CombatLogParserServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers shared analysis services required by all hero analyzers:");
        sb.AppendLine("    /// EventEmitter, base modules (Combatants, StatTracker, etc.), and base normalizers.");
        sb.AppendLine("    /// Call this once during application startup before registering any hero-specific analysis.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddCoreAnalysis(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddScoped<EventEmitter>();");
        foreach (var m in info.OwnModules)
            sb.AppendLine("        services.AddScoped<" + m.FullyQualifiedName + ">();");
        foreach (var n in info.NormalizerTypes)
            sb.AppendLine("        services.AddScoped<" + n.FullyQualifiedName + ">();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        ctx.AddSource(info.ClassName + ".g.cs", sb.ToString());
    }


    private sealed class TypeInfo
    {
        public TypeInfo(
            string name,
            string ns,
            bool extendsAbilities = false,
            string? reportTypeFullyQualified = null,
            string? activePredicateFullyQualified = null,
            ImmutableArray<string> beforeModules = default,
            ImmutableArray<string> afterModules = default)
        {
            Name = name;
            Namespace = ns;
            ExtendsAbilities = extendsAbilities;
            ReportTypeFullyQualified = reportTypeFullyQualified;
            ActivePredicateFullyQualified = activePredicateFullyQualified;
            BeforeModules = beforeModules.IsDefault ? ImmutableArray<string>.Empty : beforeModules;
            AfterModules = afterModules.IsDefault ? ImmutableArray<string>.Empty : afterModules;
        }
        public string Name { get; }
        public string Namespace { get; }
        /// <summary>True when this module extends FellowshipAnalyzer.Core.Analysis.Abilities.</summary>
        public bool ExtendsAbilities { get; }
        /// <summary>Fully-qualified report record type if this module declares <c>ToReport()</c>; otherwise null.</summary>
        public string? ReportTypeFullyQualified { get; }
        /// <summary>Fully-qualified predicate type from <c>[ActiveWhen&lt;T&gt;]</c>; otherwise null.</summary>
        public string? ActivePredicateFullyQualified { get; }
        /// <summary>Fully-qualified module names this module must come before (from <c>[Before&lt;T&gt;]</c>).</summary>
        public ImmutableArray<string> BeforeModules { get; }
        /// <summary>Fully-qualified module names this module must come after (from <c>[After&lt;T&gt;]</c>).</summary>
        public ImmutableArray<string> AfterModules { get; }
        public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name;
    }

    private sealed class ParserInfo
    {
        public ParserInfo(
            string cn, string ns,
            ImmutableArray<TypeInfo> ownModules,
            ImmutableArray<TypeInfo> baseModules,
            ImmutableArray<TypeInfo> normalizerTypes,
            ImmutableArray<TypeInfo> ownNormalizerTypes,
            string? heroEnumMember,
            bool isAbstractBase = false)
        {
            ClassName = cn;
            Namespace = ns;
            OwnModules = ownModules;
            BaseModules = baseModules;
            NormalizerTypes = normalizerTypes;
            OwnNormalizerTypes = ownNormalizerTypes;
            HeroEnumMember = heroEnumMember;
            IsAbstractBase = isAbstractBase;
        }
        public string ClassName { get; }
        public string Namespace { get; }
        /// <summary>Modules declared directly on this class.</summary>
        public ImmutableArray<TypeInfo> OwnModules { get; }
        /// <summary>Modules inherited from the base class chain (collected via GetAttributes on base symbols).</summary>
        public ImmutableArray<TypeInfo> BaseModules { get; }
        /// <summary>All normalizers (base + own) — used for the GetNormalizerTypes override.</summary>
        public ImmutableArray<TypeInfo> NormalizerTypes { get; }
        /// <summary>Normalizers declared directly on this class — used for hero-specific DI registration.</summary>
        public ImmutableArray<TypeInfo> OwnNormalizerTypes { get; }
        /// <summary>HeroName enum field name from [HeroAnalyzer] attribute (e.g. "Rime"), if present.</summary>
        public string? HeroEnumMember { get; }
        /// <summary>True when this info was collected from the abstract CombatLogParser base class.</summary>
        public bool IsAbstractBase { get; }
    }
}

