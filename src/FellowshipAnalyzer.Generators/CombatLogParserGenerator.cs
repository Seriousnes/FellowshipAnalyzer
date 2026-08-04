using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace FellowshipAnalyzer.Generators;

[Generator]
public sealed class CombatLogParserGenerator : IIncrementalGenerator
{
    private const string AddModuleAttributeShortName = "AddModuleAttribute";
    private const string AddStateAttributeShortName = "AddStateAttribute";
    private const string AddAnalyzerAttributeShortName = "AddAnalyzerAttribute";
    private const string ForPullAttributeShortName = "ForPullAttribute";
    private const string AnalyzerBaseShortName = "Analyzer";
    private const string AnalyzerSurfaceInterfaceShortName = "IAnalyzerSurface";
    private const string AddNormalizerAttributeShortName = "AddNormalizerAttribute";
    private const string HeroAnalyzerAttributeShortName = "HeroAnalyzerAttribute";
    private const string ActiveWhenAttributeShortName = "ActiveWhenAttribute";
    private const string RequiresTalentAttributeShortName = "RequiresTalentAttribute";
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
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        bool isCombatLogParserBase = symbol.IsAbstract && symbol.Name == CombatLogParserClassName;
        bool isConcreteParser = !symbol.IsAbstract && InheritsFromCombatLogParser(symbol);

        if (!isCombatLogParserBase && !isConcreteParser)
            return null;

        var ownModules = new List<TypeInfo>();
        var ownAnalyzers = new List<AnalyzerInfo>();
        var normalizerTypes = new List<TypeInfo>();

        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                if (ctx.SemanticModel.GetSymbolInfo(attr, ct).Symbol is not IMethodSymbol attrSymbol)
                    continue;

                var containingType = attrSymbol.ContainingType;
                if (!containingType.IsGenericType || containingType.TypeArguments.Length == 0)
                    continue;

                if (containingType.TypeArguments[0] is not INamedTypeSymbol typeArg)
                    continue;

                var ns = GetNamespace(typeArg);

                if (containingType.Name == AddModuleAttributeShortName
                    || containingType.Name == AddStateAttributeShortName)
                    ownModules.Add(BuildModuleTypeInfo(typeArg));
                else if (containingType.Name == AddAnalyzerAttributeShortName)
                    ownAnalyzers.Add(BuildAnalyzerInfo(typeArg));
                else if (containingType.Name == AddNormalizerAttributeShortName)
                    normalizerTypes.Add(BuildNormalizerTypeInfo(typeArg));
            }
        }

        if (ownModules.Count == 0 && ownAnalyzers.Count == 0 && normalizerTypes.Count == 0)
            return null;

        var parserNs = GetNamespace(symbol);

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
                [.. ownAnalyzers],
                [],
                isAbstractBase: true);
        }

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

        var baseModules = new List<TypeInfo>();
        var baseType = symbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            CollectModulesFromSymbol(baseType, baseModules);
            if (baseType.Name == CombatLogParserClassName) break;
            baseType = baseType.BaseType;
        }

        var baseNormalizers = new List<TypeInfo>();
        var bnType = symbol.BaseType;
        while (bnType != null && bnType.SpecialType != SpecialType.System_Object)
        {
            CollectNormalizersFromSymbol(bnType, baseNormalizers);
            if (bnType.Name == CombatLogParserClassName) break;
            bnType = bnType.BaseType;
        }

        var baseAnalyzers = new List<AnalyzerInfo>();
        var baType = symbol.BaseType;
        while (baType != null && baType.SpecialType != SpecialType.System_Object)
        {
            CollectAnalyzersFromSymbol(baType, baseAnalyzers);
            if (baType.Name == CombatLogParserClassName) break;
            baType = baType.BaseType;
        }

        return new ParserInfo(
            symbol.Name,
            parserNs,
            [.. ownModules],
            [.. baseModules],
            [.. baseNormalizers, .. normalizerTypes],
            [.. normalizerTypes],
            heroEnumMember,
            [.. ownAnalyzers],
            [.. baseAnalyzers],
            isAbstractBase: false);
    }

    private static void CollectModulesFromSymbol(INamedTypeSymbol symbol, List<TypeInfo> modules)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;
            if (attr.AttributeClass.Name != AddModuleAttributeShortName
                && attr.AttributeClass.Name != AddStateAttributeShortName) continue;
            if (!attr.AttributeClass.IsGenericType || attr.AttributeClass.TypeArguments.Length == 0) continue;

            if (attr.AttributeClass.TypeArguments[0] is not INamedTypeSymbol typeArg) continue;

            modules.Add(BuildModuleTypeInfo(typeArg));
        }
    }

    private static void CollectAnalyzersFromSymbol(INamedTypeSymbol symbol, List<AnalyzerInfo> analyzers)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;
            if (attr.AttributeClass.Name != AddAnalyzerAttributeShortName) continue;
            if (!attr.AttributeClass.IsGenericType || attr.AttributeClass.TypeArguments.Length == 0) continue;

            if (attr.AttributeClass.TypeArguments[0] is not INamedTypeSymbol typeArg) continue;

            analyzers.Add(BuildAnalyzerInfo(typeArg));
        }
    }

    /// <summary>
    /// Extracts the constructor parameters that the generator will pass when emitting
    /// <c>new T(...)</c> in <c>CreateInstance</c>. Picks the public constructor with the
    /// most parameters (single ctor in practice). For Lazy&lt;T&gt; parameters, captures
    /// the inner type so the generator can emit <c>new Lazy&lt;T&gt;(() =&gt; ...)</c>
    /// inline without a runtime <c>MakeGenericMethod</c> call.
    /// </summary>
    private static ImmutableArray<CtorParam> BuildCtorParams(INamedTypeSymbol type)
    {
        var fmt = SymbolDisplayFormat.FullyQualifiedFormat;
        var ctor = type.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsImplicitlyDeclared)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (ctor is null)
            return BuildUsesCtorParams(type, fmt);

        if (ctor.Parameters.Length == 0)
            return ImmutableArray<CtorParam>.Empty;

        var builder = ImmutableArray.CreateBuilder<CtorParam>(ctor.Parameters.Length);
        foreach (var p in ctor.Parameters)
        {
            var nullable = p.NullableAnnotation == NullableAnnotation.Annotated;
            if (p.Type is INamedTypeSymbol named
                && named.IsGenericType
                && named.Name == "Lazy"
                && named.ContainingNamespace?.ToDisplayString() == "System"
                && named.TypeArguments.Length == 1)
            {
                var inner = named.TypeArguments[0].ToDisplayString(fmt);
                var lazyFq = named.ToDisplayString(fmt);
                builder.Add(new CtorParam(lazyFq, inner, nullable));
            }
            else
            {
                builder.Add(new CtorParam(p.Type.ToDisplayString(fmt), null, nullable));
            }
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Constructor parameters synthesized from <c>[Uses&lt;T&gt;]</c> attributes when a type declares
    /// no constructor of its own. Mirrors <c>ModuleGenerator.CollectUsesDependencies</c> exactly —
    /// same fully-qualified ordering, de-duplication, and accessor-name collision skipping — so the
    /// argument list emitted here lines up with the primary constructor that generator produces. The
    /// two generators never see each other's output, so both derive the list from the same attributes.
    /// </summary>
    private static ImmutableArray<CtorParam> BuildUsesCtorParams(INamedTypeSymbol type, SymbolDisplayFormat fmt)
    {
        var depTypes = new List<INamedTypeSymbol>();
        foreach (var attr in type.GetAttributes())
        {
            var ac = attr.AttributeClass;
            if (ac is not { IsGenericType: true }) continue;
            if (ac.Name != "DependencyAttribute") continue;
            if (ac.ContainingNamespace?.ToDisplayString() != "FellowshipAnalyzer.Core.Analysis") continue;
            if (ac.TypeArguments.Length == 1 && ac.TypeArguments[0] is INamedTypeSymbol dep) depTypes.Add(dep);
        }

        if (depTypes.Count == 0)
            return ImmutableArray<CtorParam>.Empty;

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in type.GetMembers())
            usedNames.Add(member.Name);

        var builder = ImmutableArray.CreateBuilder<CtorParam>();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dep in depTypes.OrderBy(d => d.ToDisplayString(fmt), StringComparer.Ordinal))
        {
            var innerFq = dep.ToDisplayString(fmt);
            if (!seenTypes.Add(innerFq)) continue;
            if (!usedNames.Add(dep.Name)) continue;

            builder.Add(new CtorParam("global::System.Lazy<" + innerFq + ">", innerFq, nullable: false));
        }

        return builder.ToImmutable();
    }

    private const string ParseContextFqn = "global::FellowshipAnalyzer.Core.Analysis.ParseContext";
    private const string EventReadOnlyListFqn = "global::System.Collections.Generic.IReadOnlyList<global::FellowshipAnalyzer.Core.Events.Event>";

    /// <summary>
    /// Emits the C# expression for a single constructor argument. Routes by parameter kind:
    /// <list type="bullet">
    /// <item><c>Lazy&lt;T&gt;</c> deps build inline via <c>ResolveAnalysisModule</c>.</item>
    /// <item><see cref="ParseContextFqn"/> reads from the parser's <c>CurrentParseContext</c>.</item>
    /// <item><see cref="EventReadOnlyListFqn"/> reads from the parser's <c>Events</c> property.</item>
    /// <item>Module-type deps resolve via <c>ResolveAnalysisModule</c>.</item>
    /// <item>Everything else falls back to the outer DI container via <c>Provider</c>.</item>
    /// </list>
    /// </summary>
    private static string EmitCtorArg(CtorParam p, HashSet<string> moduleTypeFqns)
    {
        if (p.LazyInnerFullyQualified != null)
        {
            var inner = p.LazyInnerFullyQualified;
            return "new " + p.FullyQualified + "(() => (" + inner + ")ResolveAnalysisModule(typeof(" + inner + ")))";
        }
        if (p.FullyQualified == ParseContextFqn)
            return "CurrentParseContext";
        if (p.FullyQualified == EventReadOnlyListFqn)
            return "Events";
        if (moduleTypeFqns.Contains(p.FullyQualified))
        {
            var castType = p.Nullable ? p.FullyQualified + "?" : p.FullyQualified;
            return "(" + castType + ")ResolveAnalysisModule(typeof(" + p.FullyQualified + "))";
        }
        var outerCast = p.Nullable ? p.FullyQualified + "?" : p.FullyQualified;
        var bang = p.Nullable ? "" : "!";
        return "(" + outerCast + ")Provider.GetService(typeof(" + p.FullyQualified + "))" + bang;
    }

    private static void EmitCreateInstanceBody(StringBuilder sb, IEnumerable<TypeInfo> types, string indent, HashSet<string> moduleTypeFqns)
    {
        foreach (var t in types)
            EmitCreateInstanceCase(sb, t.FullyQualifiedName, t.CtorParams, indent, moduleTypeFqns);
    }

    private static void EmitCreateInstanceCase(StringBuilder sb, string fqn, ImmutableArray<CtorParam> ctorParams, string indent, HashSet<string> moduleTypeFqns)
    {
        sb.Append(indent).Append("if (type == typeof(global::").Append(fqn).AppendLine("))");
        if (ctorParams.Length == 0)
        {
            sb.Append(indent).Append("    return new global::").Append(fqn).AppendLine("();");
        }
        else
        {
            sb.Append(indent).Append("    return new global::").Append(fqn).Append('(');
            for (var i = 0; i < ctorParams.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(EmitCtorArg(ctorParams[i], moduleTypeFqns));
            }
            sb.AppendLine(");");
        }
    }

    /// <summary>
    /// Builds a <see cref="TypeInfo"/> for a module type, capturing all module-level metadata
    /// (Abilities inheritance, <c>[ActiveWhen&lt;T&gt;]</c> predicate, <c>[RequiresTalent(id)]</c>
    /// gates, and <c>[Before&lt;T&gt;]</c> / <c>[After&lt;T&gt;]</c> ordering constraints).
    /// </summary>
    private static TypeInfo BuildModuleTypeInfo(INamedTypeSymbol moduleType)
    {
        var ns = GetNamespace(moduleType);
        var extendsAbilities = InheritsFromAbilities(moduleType);

        string? activePredicate = null;
        var beforeFqns = new List<string>();
        var afterFqns = new List<string>();
        var requiredTalentIds = new List<int>();

        foreach (var attr in moduleType.GetAttributes())
        {
            var ac = attr.AttributeClass;
            if (ac == null) continue;

            if (ac.Name == RequiresTalentAttributeShortName && !ac.IsGenericType)
            {
                if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is int talentId)
                    requiredTalentIds.Add(talentId);
                continue;
            }

            if (!ac.IsGenericType || ac.TypeArguments.Length == 0) continue;

            if (ac.TypeArguments[0] is not INamedTypeSymbol arg) continue;

            var argFqn = FullyQualifiedName(arg);

            if (ac.Name == ActiveWhenAttributeShortName)
                activePredicate = argFqn;
            else if (ac.Name == BeforeAttributeShortName)
                beforeFqns.Add(argFqn);
            else if (ac.Name == AfterAttributeShortName)
                afterFqns.Add(argFqn);
        }

        return new TypeInfo(moduleType.Name, ns, extendsAbilities, activePredicate,
            [.. beforeFqns], [.. afterFqns], BuildCtorParams(moduleType), [.. requiredTalentIds]);
    }

    private static TypeInfo BuildNormalizerTypeInfo(INamedTypeSymbol normalizerType)
    {
        return new TypeInfo(
            normalizerType.Name,
            GetNamespace(normalizerType),
            ctorParams: BuildCtorParams(normalizerType));
    }

    /// <summary>
    /// Builds an <see cref="AnalyzerInfo"/> for a pull-lifetime analyzer: its constructor
    /// parameters (for <c>CreateInstance</c>), the surface type it is exposed under on pull read
    /// paths, the <c>[ForPull]</c> match filter that gates which pulls it runs on, and any
    /// <c>[RequiresTalent(id)]</c> gates.
    /// </summary>
    private static AnalyzerInfo BuildAnalyzerInfo(INamedTypeSymbol analyzerType)
    {
        var (surfaceFqn, surfaceMemberName) = GetAnalyzerSurfaceType(analyzerType);

        var targets = 0;
        var boss = 0;
        var seenForPull = false;
        var requiredTalentIds = new List<int>();
        string? activePredicate = null;
        foreach (var attr in analyzerType.GetAttributes())
        {
            var ac = attr.AttributeClass;
            if (ac == null) continue;

            if (ac.Name == RequiresTalentAttributeShortName && !ac.IsGenericType)
            {
                if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is int talentId)
                    requiredTalentIds.Add(talentId);
                continue;
            }

            if (ac.Name == ActiveWhenAttributeShortName && ac.IsGenericType && ac.TypeArguments.Length > 0)
            {
                if (ac.TypeArguments[0] is INamedTypeSymbol predicate)
                    activePredicate = FullyQualifiedName(predicate);
                continue;
            }

            if (ac.Name != ForPullAttributeShortName || seenForPull) continue;

            seenForPull = true;
            if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is int t)
                targets = t;
            foreach (var na in attr.NamedArguments)
            {
                if (na.Key == "Boss" && na.Value.Value is int b) boss = b;
            }
        }

        return new AnalyzerInfo(
            analyzerType.Name,
            GetNamespace(analyzerType),
            BuildCtorParams(analyzerType),
            surfaceFqn,
            surfaceMemberName,
            targets,
            boss,
            [.. requiredTalentIds],
            activePredicate);
    }

    /// <summary>
    /// Resolves the analyzer's surface as <c>(fully-qualified type, member base name)</c>. The type
    /// is the analyzer's <c>IAnalyzerSurface</c> marker interface if it implements one, otherwise the
    /// topmost ancestor deriving directly from <c>Analyzer</c>. The member base name drives the
    /// generated read-path identifiers: the surface class's simple name, or an interface's simple name
    /// with a leading <c>I</c> stripped (<c>ISearingBlazeAnalyzer</c> -&gt; <c>SearingBlazeAnalyzer</c>).
    /// Mirrors the runtime <c>Analyzer.GetSurfaceType</c>.
    /// </summary>
    private static (string Fqn, string MemberName) GetAnalyzerSurfaceType(INamedTypeSymbol analyzerType)
    {
        var fmt = SymbolDisplayFormat.FullyQualifiedFormat;

        if (FindSurfaceInterface(analyzerType) is { } surfaceInterface)
            return (surfaceInterface.ToDisplayString(fmt), StripLeadingI(surfaceInterface.Name));

        var current = analyzerType;
        while (current.BaseType is { } baseType
            && baseType.SpecialType != SpecialType.System_Object
            && baseType.Name != AnalyzerBaseShortName)
        {
            current = baseType;
        }
        return (current.ToDisplayString(fmt), current.Name);
    }

    /// <summary>
    /// The single surface marker interface <paramref name="analyzerType"/> implements, or null. A
    /// surface interface is any interface, other than <c>IAnalyzerSurface</c> itself, that implements
    /// <c>IAnalyzerSurface</c>. FA0017 enforces at most one, so the first match is unambiguous.
    /// </summary>
    private static INamedTypeSymbol? FindSurfaceInterface(INamedTypeSymbol analyzerType)
    {
        foreach (var iface in analyzerType.AllInterfaces)
        {
            if (iface.Name == AnalyzerSurfaceInterfaceShortName) continue;
            foreach (var baseIface in iface.AllInterfaces)
            {
                if (baseIface.Name == AnalyzerSurfaceInterfaceShortName)
                    return iface;
            }
        }
        return null;
    }

    private static string StripLeadingI(string name) =>
        name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]) ? name.Substring(1) : name;

    private static string FullyQualifiedName(INamedTypeSymbol t)
    {
        var ns = GetNamespace(t);
        return string.IsNullOrEmpty(ns) ? t.Name : ns + "." + t.Name;
    }

    private static void CollectNormalizersFromSymbol(INamedTypeSymbol symbol, List<TypeInfo> normalizers)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;
            if (attr.AttributeClass.Name != AddNormalizerAttributeShortName) continue;
            if (!attr.AttributeClass.IsGenericType || attr.AttributeClass.TypeArguments.Length == 0) continue;

            if (attr.AttributeClass.TypeArguments[0] is not INamedTypeSymbol typeArg) continue;

            normalizers.Add(BuildNormalizerTypeInfo(typeArg));
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

        if (sorted.Count != declarationOrder.Count)
        {
            var emitted = new HashSet<TypeInfo>(sorted);
            foreach (var m in declarationOrder)
                if (emitted.Add(m)) sorted.Add(m);
        }

        return sorted;
    }

    /// <summary>
    /// Emits an <c>IsModuleActive</c> override that switches on the module Type and returns its
    /// activation expression: the static <c>IsActive(ParseContext)</c> call of an
    /// <c>[ActiveWhen&lt;T&gt;]</c> predicate and/or a <c>HasTalent</c> check per
    /// <c>[RequiresTalent(id)]</c>, combined with <c>&amp;&amp;</c>. Only emitted when at least one
    /// module in <paramref name="orderedModules"/> declared one of those attributes. A module with
    /// only <c>[ActiveWhen&lt;T&gt;]</c> emits the same single-call expression as before.
    /// </summary>
    private static void EmitIsModuleActive(StringBuilder sb, List<TypeInfo> orderedModules)
    {
        var gated = orderedModules.FindAll(m => m.ActivePredicateFullyQualified != null || m.RequiredTalentIds.Length > 0);
        if (gated.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("    protected override bool IsModuleActive(global::System.Type moduleType, global::FellowshipAnalyzer.Core.Analysis.ParseContext context)");
        sb.AppendLine("    {");
        foreach (var m in gated)
        {
            sb.AppendLine("        if (moduleType == typeof(global::" + m.FullyQualifiedName + "))");
            sb.AppendLine("            return " + BuildActivationExpression(m) + ";");
        }
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Builds the boolean activation expression for a gated module: the <c>[ActiveWhen&lt;T&gt;]</c>
    /// predicate call (if any) followed by one <c>context.SelectedCombatant.HasTalent(id)</c> term
    /// per <c>[RequiresTalent(id)]</c>, joined with <c>&amp;&amp;</c>. With only a predicate this
    /// returns the bare <c>IsActive</c> call — byte-identical to the pre-<c>[RequiresTalent]</c> output.
    /// </summary>
    private static string BuildActivationExpression(TypeInfo m)
    {
        var parts = new List<string>();
        if (m.ActivePredicateFullyQualified != null)
            parts.Add("global::" + m.ActivePredicateFullyQualified + ".IsActive(context)");
        foreach (var id in m.RequiredTalentIds)
            parts.Add("context.SelectedCombatant.HasTalent(" + id.ToString(CultureInfo.InvariantCulture) + ")");
        return string.Join(" && ", parts);
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

        sb.AppendLine("    public " + info.ClassName + "(EventEmitter emitter, IServiceProvider provider) : base(emitter, provider) { }");
        sb.AppendLine();

        if (info.HeroEnumMember != null)
        {
            sb.AppendLine("    public override global::FellowshipAnalyzer.Core.Analysis.Hero? Hero => global::FellowshipAnalyzer.Core.Analysis.Hero." + info.HeroEnumMember + ";");
            sb.AppendLine();
        }

        foreach (var m in info.OwnModules)
        {
            var propName = StripSuffix(m.Name, "Analyzer");
            sb.AppendLine("    public " + m.FullyQualifiedName + "? " + propName + " => GetModule<" + m.FullyQualifiedName + ">();");
        }

        sb.AppendLine();

        var orderedModules = TopologicalSort(info.BaseModules, info.OwnModules);
        sb.AppendLine("    protected override Type[] GetModuleTypes() =>");
        sb.AppendLine("    [");
        foreach (var m in orderedModules)
            sb.AppendLine("        typeof(" + m.FullyQualifiedName + "),");
        sb.AppendLine("    ];");

        EmitIsModuleActive(sb, orderedModules);

        if (info.NormalizerTypes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    protected override Type[] GetNormalizerTypes() =>");
            sb.AppendLine("    [");
            foreach (var n in info.NormalizerTypes)
                sb.AppendLine("        typeof(" + n.FullyQualifiedName + "),");
            sb.AppendLine("    ];");
        }

        var moduleTypeFqns = new HashSet<string>();
        foreach (var m in info.BaseModules) moduleTypeFqns.Add("global::" + m.FullyQualifiedName);
        foreach (var m in info.OwnModules) moduleTypeFqns.Add("global::" + m.FullyQualifiedName);

        if (info.OwnModules.Length > 0 || info.OwnNormalizerTypes.Length > 0 || info.OwnAnalyzers.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    protected override object? CreateInstance(global::System.Type type)");
            sb.AppendLine("    {");
            EmitCreateInstanceBody(sb, info.OwnModules, "        ", moduleTypeFqns);
            EmitCreateInstanceBody(sb, info.OwnNormalizerTypes, "        ", moduleTypeFqns);
            foreach (var a in info.OwnAnalyzers)
                EmitCreateInstanceCase(sb, a.FullyQualifiedName, a.CtorParams, "        ", moduleTypeFqns);
            sb.AppendLine("        return base.CreateInstance(type);");
            sb.AppendLine("    }");
        }

        var surfaceTypes = DistinctSurfaceTypes(info.AllAnalyzers);
        EmitAnalyzerSurface(sb, info, surfaceTypes);

        var parserBaseName = StripSuffix(info.ClassName, "CombatLogParser");

        sb.AppendLine("}");
        sb.AppendLine();

        EmitPullReadPaths(sb, parserBaseName, surfaceTypes);

        sb.AppendLine("public static class " + parserBaseName + "ServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection Add" + parserBaseName + "Analysis(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddTransient<" + info.ClassName + ">();");
        if (info.HeroEnumMember != null)
        {
            sb.AppendLine("        services.AddKeyedTransient<IHeroAnalyzer>(global::FellowshipAnalyzer.Core.Analysis.HeroName." + info.HeroEnumMember + ", (sp, _) => sp.GetRequiredService<" + info.ClassName + ">());");
        }
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        ctx.AddSource(info.ClassName + ".g.cs", sb.ToString());
    }

    /// <summary>Distinct analyzer surface types in declaration order, deduped by fully-qualified type.</summary>
    private static List<(string Fqn, string MemberName)> DistinctSurfaceTypes(IEnumerable<AnalyzerInfo> analyzers)
    {
        var result = new List<(string, string)>();
        var seen = new HashSet<string>();
        foreach (var a in analyzers)
        {
            if (seen.Add(a.SurfaceTypeFullyQualified))
                result.Add((a.SurfaceTypeFullyQualified, a.SurfaceTypeMemberName));
        }
        return result;
    }

    private static string AnalyzerListFieldName(string simpleName) => "_" + LowerFirst(simpleName) + "s";

    private static string LowerFirst(string s) =>
        s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    /// <summary>
    /// Emits the <c>[ForPull]</c> match expression: the compile-time-constant
    /// <c>(mask).HasFlag(pull.Targets)</c> with an optional boss clause, followed by one
    /// <c>SelectedCombatant.HasTalent(id)</c> term per <c>[RequiresTalent(id)]</c> on the analyzer.
    /// The declared mask is the receiver: a pull carries exactly one
    /// <see cref="FellowshipAnalyzer.Core.Analysis.PullKind"/>, so the test is that the declared set
    /// contains it.
    /// An analyzer with no talent gate emits output byte-identical to the pre-<c>[RequiresTalent]</c>
    /// form. The talent terms read the parse context populated before any pull opens, so a build
    /// without the talent never constructs the analyzer and its read paths stay empty.
    /// </summary>
    private static string EmitForPullGate(AnalyzerInfo a)
    {
        const string pk = "global::FellowshipAnalyzer.Core.Analysis.PullKind";
        var flags = new List<string>();
        if ((a.ForPullTargets & 1) != 0) flags.Add(pk + ".Single");
        if ((a.ForPullTargets & 2) != 0) flags.Add(pk + ".Multi");
        var mask = flags.Count > 0 ? string.Join(" | ", flags) : "(" + pk + ")0";
        var gate = "(" + mask + ").HasFlag(pull.Targets)";
        if (a.ForPullBoss == 1) gate += " && pull.IsBoss";
        else if (a.ForPullBoss == 2) gate += " && !pull.IsBoss";
        foreach (var id in a.RequiredTalentIds)
            gate += " && SelectedCombatant.HasTalent(" + id.ToString(CultureInfo.InvariantCulture) + ")";
        if (a.ActivePredicate is { } predicate)
            gate += " && global::" + predicate + ".IsActive(CurrentParseContext)";
        return gate;
    }

    /// <summary>
    /// Emits the in-class analyzer surface: the per-pull <c>GetAnalyzerTypes(Pull)</c> gate, the
    /// typed per-surface cross-pull index (backing list + read-only property), the
    /// <c>IndexPullAnalyzer</c> router, and the <c>For(pull)</c> per-pull view accessor.
    /// </summary>
    private static void EmitAnalyzerSurface(StringBuilder sb, ParserInfo info, List<(string Fqn, string MemberName)> surfaceTypes)
    {
        var analyzers = info.AllAnalyzers.ToList();
        if (analyzers.Count == 0) return;

        var parserBaseName = StripSuffix(info.ClassName, "CombatLogParser");
        const string pull = "global::FellowshipAnalyzer.Core.Analysis.Pull";

        sb.AppendLine();
        sb.AppendLine("    protected override global::System.Type[] GetAnalyzerTypes(" + pull + " pull)");
        sb.AppendLine("    {");
        sb.AppendLine("        var __analyzers = new global::System.Collections.Generic.List<global::System.Type>();");
        foreach (var a in analyzers)
            sb.AppendLine("        if (" + EmitForPullGate(a) + ") __analyzers.Add(typeof(global::" + a.FullyQualifiedName + "));");
        sb.AppendLine("        return [.. __analyzers];");
        sb.AppendLine("    }");

        if (surfaceTypes.Count == 0) return;

        sb.AppendLine();
        foreach (var st in surfaceTypes)
        {
            var field = AnalyzerListFieldName(st.MemberName);
            sb.AppendLine("    private readonly global::FellowshipAnalyzer.Core.Analysis.PullAnalyzerList<" + st.Fqn + "> " + field + " = new();");
            sb.AppendLine("    public global::System.Collections.Generic.IReadOnlyList<global::FellowshipAnalyzer.Core.Analysis.PullAnalyzer<" + st.Fqn + ">> " + st.MemberName + "s => ClampToSelectedPull(" + field + ");");
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void IndexPullAnalyzer(" + pull + " pull, global::FellowshipAnalyzer.Core.Analysis.Analyzer analyzer)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (analyzer)");
        sb.AppendLine("        {");
        var caseIndex = 0;
        foreach (var st in surfaceTypes)
        {
            var local = "__a" + caseIndex++;
            sb.AppendLine("            case " + st.Fqn + " " + local + ":");
            sb.AppendLine("                " + AnalyzerListFieldName(st.MemberName) + ".Add(new global::FellowshipAnalyzer.Core.Analysis.PullAnalyzer<" + st.Fqn + ">(pull, " + local + "));");
            sb.AppendLine("                break;");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine("    public " + parserBaseName + "PullView For(" + pull + " pull) => new(pull);");
    }

    /// <summary>
    /// Emits the namespace-level per-pull read paths: a <c>{Parser}PullView</c> struct exposing
    /// each retained analyzer by surface type, and a <c>{Parser}PullExtensions</c> class adding the
    /// <c>pull.{Analyzer}</c> extension property over <see cref="FellowshipAnalyzer.Core.Analysis.Pull"/>.
    /// </summary>
    private static void EmitPullReadPaths(StringBuilder sb, string parserBaseName, List<(string Fqn, string MemberName)> surfaceTypes)
    {
        if (surfaceTypes.Count == 0) return;
        const string pull = "global::FellowshipAnalyzer.Core.Analysis.Pull";

        sb.Append("public readonly struct ").Append(parserBaseName).Append("PullView(").Append(pull).AppendLine(" pull)");
        sb.AppendLine("{");
        foreach (var st in surfaceTypes)
            sb.Append("    public ").Append(st.Fqn).Append("? ").Append(st.MemberName)
              .Append(" => (").Append(st.Fqn).Append("?)pull.GetAnalyzer(typeof(").Append(st.Fqn).AppendLine("));");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.Append("public static class ").Append(parserBaseName).AppendLine("PullExtensions");
        sb.AppendLine("{");
        sb.Append("    extension(").Append(pull).AppendLine(" pull)");
        sb.AppendLine("    {");
        foreach (var st in surfaceTypes)
            sb.Append("        public ").Append(st.Fqn).Append("? ").Append(st.MemberName)
              .Append(" => (").Append(st.Fqn).Append("?)pull.GetAnalyzer(typeof(").Append(st.Fqn).AppendLine("));");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates the abstract base partial for <c>CombatLogParser</c>. Emits the typed
    /// module accessors, the <c>CreateInstance</c> factory for base-declared modules and
    /// normalizers, and the <c>AddCoreAnalysis</c> DI extension. Modules and normalizers are
    /// constructed per-analysis via <c>CreateInstance</c> and are never registered in the
    /// outer DI container.
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

        sb.AppendLine("public abstract partial class " + info.ClassName);
        sb.AppendLine("{");
        foreach (var m in info.OwnModules)
        {
            var propName = StripSuffix(m.Name, "Analyzer");
            sb.AppendLine("    /// <summary>The <see cref=\"global::" + m.FullyQualifiedName +
                          "\"/> module for this analysis, or <c>null</c> when it is not active.</summary>");
            sb.AppendLine("    public " + m.FullyQualifiedName + "? " + propName + " => GetModule<" + m.FullyQualifiedName + ">();");
        }

        var baseModuleTypeFqns = new HashSet<string>();
        foreach (var m in info.OwnModules) baseModuleTypeFqns.Add("global::" + m.FullyQualifiedName);

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Per-analysis factory hook. Source-generated on the abstract base for every module and");
        sb.AppendLine("    /// normalizer declared via <c>[AddModule]</c> / <c>[AddNormalizer]</c> on this class.");
        sb.AppendLine("    /// Concrete hero parsers override this and chain to <c>base.CreateInstance</c>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    protected virtual object? CreateInstance(global::System.Type type)");
        sb.AppendLine("    {");
        EmitCreateInstanceBody(sb, info.OwnModules, "        ", baseModuleTypeFqns);
        EmitCreateInstanceBody(sb, info.OwnNormalizerTypes, "        ", baseModuleTypeFqns);
        foreach (var a in info.OwnAnalyzers)
            EmitCreateInstanceCase(sb, a.FullyQualifiedName, a.CtorParams, "        ", baseModuleTypeFqns);
        sb.AppendLine("        return null;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>Dependency-injection registration for the analysis services every hero parser needs.</summary>");
        sb.AppendLine("public static class CombatLogParserServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers shared analysis services required by all hero analyzers.");
        sb.AppendLine("    /// Call this once during application startup before registering any hero-specific analysis.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddCoreAnalysis(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddTransient<EventEmitter>();");
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
            string? activePredicateFullyQualified = null,
            ImmutableArray<string> beforeModules = default,
            ImmutableArray<string> afterModules = default,
            ImmutableArray<CtorParam> ctorParams = default,
            ImmutableArray<int> requiredTalentIds = default)
        {
            Name = name;
            Namespace = ns;
            ExtendsAbilities = extendsAbilities;
            ActivePredicateFullyQualified = activePredicateFullyQualified;
            BeforeModules = beforeModules.IsDefault ? ImmutableArray<string>.Empty : beforeModules;
            AfterModules = afterModules.IsDefault ? ImmutableArray<string>.Empty : afterModules;
            CtorParams = ctorParams.IsDefault ? ImmutableArray<CtorParam>.Empty : ctorParams;
            RequiredTalentIds = requiredTalentIds.IsDefault ? ImmutableArray<int>.Empty : requiredTalentIds;
        }
        public string Name { get; }
        public string Namespace { get; }
        /// <summary>True when this module extends FellowshipAnalyzer.Core.Analysis.Abilities.</summary>
        public bool ExtendsAbilities { get; }
        /// <summary>Fully-qualified predicate type from <c>[ActiveWhen&lt;T&gt;]</c>; otherwise null.</summary>
        public string? ActivePredicateFullyQualified { get; }
        /// <summary>Fully-qualified module names this module must come before (from <c>[Before&lt;T&gt;]</c>).</summary>
        public ImmutableArray<string> BeforeModules { get; }
        /// <summary>Fully-qualified module names this module must come after (from <c>[After&lt;T&gt;]</c>).</summary>
        public ImmutableArray<string> AfterModules { get; }
        /// <summary>Native talent ids the selected combatant must have (from <c>[RequiresTalent(id)]</c>), in declaration order.</summary>
        public ImmutableArray<int> RequiredTalentIds { get; }
        /// <summary>Parameters of the public constructor selected for generator-emitted construction.</summary>
        public ImmutableArray<CtorParam> CtorParams { get; }
        public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name;
    }

    private sealed class CtorParam
    {
        public CtorParam(string fullyQualified, string? lazyInnerFullyQualified, bool nullable)
        {
            FullyQualified = fullyQualified;
            LazyInnerFullyQualified = lazyInnerFullyQualified;
            Nullable = nullable;
        }
        /// <summary>The parameter's type, fully qualified with <c>global::</c> prefix.</summary>
        public string FullyQualified { get; }
        /// <summary>For <c>Lazy&lt;T&gt;</c> parameters, the inner T fully qualified; otherwise null.</summary>
        public string? LazyInnerFullyQualified { get; }
        /// <summary>True when the parameter has a nullable reference type annotation.</summary>
        public bool Nullable { get; }
    }

    private sealed class ParserInfo(
        string cn, string ns,
        ImmutableArray<TypeInfo> ownModules,
        ImmutableArray<TypeInfo> baseModules,
        ImmutableArray<TypeInfo> normalizerTypes,
        ImmutableArray<TypeInfo> ownNormalizerTypes,
        string? heroEnumMember,
        ImmutableArray<AnalyzerInfo> ownAnalyzers,
        ImmutableArray<AnalyzerInfo> baseAnalyzers,
        bool isAbstractBase = false)
    {
        public string ClassName { get; } = cn;
        public string Namespace { get; } = ns;
        /// <summary>Modules declared directly on this class.</summary>
        public ImmutableArray<TypeInfo> OwnModules { get; } = ownModules;
        /// <summary>Modules inherited from the base class chain (collected via GetAttributes on base symbols).</summary>
        public ImmutableArray<TypeInfo> BaseModules { get; } = baseModules;
        /// <summary>All normalizers (base + own) — used for the GetNormalizerTypes override.</summary>
        public ImmutableArray<TypeInfo> NormalizerTypes { get; } = normalizerTypes;
        /// <summary>Normalizers declared directly on this class — used for hero-specific DI registration.</summary>
        public ImmutableArray<TypeInfo> OwnNormalizerTypes { get; } = ownNormalizerTypes;
        /// <summary>HeroName enum field name from [HeroAnalyzer] attribute (e.g. "Rime"), if present.</summary>
        public string? HeroEnumMember { get; } = heroEnumMember;
        /// <summary>Pull-lifetime analyzers declared directly on this class via <c>[AddAnalyzer]</c>.</summary>
        public ImmutableArray<AnalyzerInfo> OwnAnalyzers { get; } = ownAnalyzers;
        /// <summary>Pull-lifetime analyzers inherited from the base class chain.</summary>
        public ImmutableArray<AnalyzerInfo> BaseAnalyzers { get; } = baseAnalyzers;
        /// <summary>All analyzers (base + own) in declaration order.</summary>
        public IEnumerable<AnalyzerInfo> AllAnalyzers => BaseAnalyzers.Concat(OwnAnalyzers);
        /// <summary>True when this info was collected from the abstract CombatLogParser base class.</summary>
        public bool IsAbstractBase { get; } = isAbstractBase;
    }

    private sealed class AnalyzerInfo
    {
        public AnalyzerInfo(
            string name,
            string ns,
            ImmutableArray<CtorParam> ctorParams,
            string surfaceTypeFullyQualified,
            string surfaceTypeMemberName,
            int forPullTargets,
            int forPullBoss,
            ImmutableArray<int> requiredTalentIds,
            string? activePredicate)
        {
            ActivePredicate = activePredicate;
            Name = name;
            Namespace = ns;
            CtorParams = ctorParams.IsDefault ? ImmutableArray<CtorParam>.Empty : ctorParams;
            SurfaceTypeFullyQualified = surfaceTypeFullyQualified;
            SurfaceTypeMemberName = surfaceTypeMemberName;
            ForPullTargets = forPullTargets;
            ForPullBoss = forPullBoss;
            RequiredTalentIds = requiredTalentIds.IsDefault ? ImmutableArray<int>.Empty : requiredTalentIds;
        }
        public string Name { get; }
        public string Namespace { get; }
        public ImmutableArray<CtorParam> CtorParams { get; }
        /// <summary>Fully-qualified surface type: the surface marker interface, or the topmost ancestor deriving directly from <c>Analyzer</c>.</summary>
        public string SurfaceTypeFullyQualified { get; }
        /// <summary>Member base name for the surface's read paths (e.g. "WintersEmbraceAnalyzer"): the surface class's simple name, or an interface's name with a leading <c>I</c> stripped.</summary>
        public string SurfaceTypeMemberName { get; }
        /// <summary>The <c>[ForPull]</c> target bitmask (<c>PullKind</c> as int).</summary>
        public int ForPullTargets { get; }
        /// <summary><c>[ForPull(Boss = …)]</c> as <c>PullBoss</c> int: 0 = Either, 1 = Boss, 2 = NonBoss.</summary>
        public int ForPullBoss { get; }
        /// <summary>Native talent ids the selected combatant must have (from <c>[RequiresTalent(id)]</c>), in declaration order.</summary>
        public ImmutableArray<int> RequiredTalentIds { get; }
        /// <summary>Fully-qualified predicate type from <c>[ActiveWhen&lt;T&gt;]</c>, or <c>null</c> when the analyzer is unconditional.</summary>
        public string? ActivePredicate { get; }
        public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name;
    }
}

