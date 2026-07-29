using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Emits the per-module generated partial: a <c>RegisterAttributeSubscriptions</c> override
/// for any <see cref="OnAttribute{TEvent}"/> handlers declared on the class, and a cached
/// private accessor for every primary-constructor parameter of type <c>Lazy&lt;TModule&gt;</c>.
/// One hand-written module file produces exactly one generated partial file.
/// <para>
/// The override always chains to <c>base.RegisterAttributeSubscriptions()</c> first, so a module
/// that declares its own handlers keeps the ones its base class declares. The base implementation on
/// <c>EventSubscriber</c> is empty, so the call costs nothing when there is nothing to inherit.
/// </para>
/// </summary>
[Generator]
public sealed class ModuleGenerator : IIncrementalGenerator
{
    private const string OnAttributeShortName = "On";
    private const string OnAttributeFullName = "OnAttribute";
    private const string DependencyAttributeShortName = "Dependency";
    private const string DependencyAttributeFullName = "DependencyAttribute";
    private const string AnalysisNamespace = "FellowshipAnalyzer.Core.Analysis";
    private const string SpellsNamespace = "FellowshipAnalyzer.Core.Common.Spells";
    private const string SpellRegistryInterfaceName = "ISpellRegistry";
    private const string SpellTypeName = "Spell";
    private const string EffectTypeName = "Effect";

    private static readonly DiagnosticDescriptor SpellArgMustBeNameOfDescriptor = new(
        id: "FA0002",
        title: "OnAttribute spell argument must be nameof(Registry.Member)",
        messageFormat: "OnAttribute '{0}' argument must be a `nameof(Registry.Member)` expression",
        category: "Module",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SpellArgNotRegistryMemberDescriptor = new(
        id: "FA0003",
        title: "OnAttribute spell argument is not a registered spell",
        messageFormat: "'{0}' is not a static Spell/Effect property on a type implementing ISpellRegistry",
        category: "Module",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SpellArgInitializerUnresolvableDescriptor = new(
        id: "FA0004",
        title: "OnAttribute spell argument has no resolvable Id",
        messageFormat: "Could not recover an int Id literal from the constructor of '{0}'",
        category: "Module",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DependencyIgnoredDescriptor = new(
        id: "FA0018",
        title: "[Dependency<T>] dependency ignored",
        messageFormat: "[Dependency<{0}>] on '{1}' was ignored ({2})",
        category: "Module",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var modules = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => GetModuleInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        context.RegisterSourceOutput(modules, Emit);
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        var isPartial = false;
        foreach (var modifier in classDecl.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                isPartial = true;
                break;
            }
        }
        if (!isPartial) return false;

        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                if (GetAttributeShortName(attr) is DependencyAttributeShortName or DependencyAttributeFullName)
                    return true;
            }
        }

        foreach (var member in classDecl.Members)
        {
            if (member is not MethodDeclarationSyntax method) continue;
            foreach (var attrList in method.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    if (GetAttributeShortName(attr) is { } name &&
                        (name == OnAttributeShortName || name == OnAttributeFullName))
                    {
                        return true;
                    }
                }
            }
        }

        if (classDecl.ParameterList is { Parameters.Count: > 0 })
            return true;

        return false;
    }

    private static string? GetAttributeShortName(AttributeSyntax attr)
    {
        var nameSyntax = attr.Name;
        while (true)
        {
            switch (nameSyntax)
            {
                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText;
                case SimpleNameSyntax simple:
                    return simple.Identifier.ValueText;
                case QualifiedNameSyntax qualified:
                    nameSyntax = qualified.Right;
                    continue;
                case AliasQualifiedNameSyntax aliased:
                    nameSyntax = aliased.Name;
                    continue;
                default:
                    return null;
            }
        }
    }

    private static ModuleInfo? GetModuleInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        var declRefs = symbol.DeclaringSyntaxReferences;
        if (declRefs.Length > 1)
        {
            string? earliestPath = null;
            foreach (var declRef in declRefs)
            {
                var path = declRef.SyntaxTree.FilePath ?? string.Empty;
                if (earliestPath is null || string.CompareOrdinal(path, earliestPath) < 0)
                    earliestPath = path;
            }
            var currentPath = classDecl.SyntaxTree.FilePath ?? string.Empty;
            if (!string.Equals(currentPath, earliestPath, StringComparison.Ordinal))
                return null;
        }

        var inheritsEventSubscriber = InheritsFromEventSubscriber(symbol);

        var handlers = ImmutableArray.CreateBuilder<HandlerInfo>();
        var diagnostics = ImmutableArray.CreateBuilder<PendingDiagnostic>();
        if (inheritsEventSubscriber)
        {
            foreach (var member in symbol.GetMembers())
            {
                if (member is not IMethodSymbol method) continue;
                foreach (var attrData in method.GetAttributes())
                {
                    if (!IsOnAttribute(attrData)) continue;
                    if (attrData.AttributeClass is not INamedTypeSymbol attrClass) continue;
                    if (attrClass.TypeArguments.Length != 1) continue;
                    if (attrClass.TypeArguments[0] is not INamedTypeSymbol eventType) continue;

                    var handler = BuildHandler(method, attrClass, eventType, attrData, ctx.SemanticModel, diagnostics, ct);
                    if (handler is not null)
                        handlers.Add(handler);
                }
            }
        }

        var lazyAccessors = CollectLazyAccessors(symbol);
        var (usesDeps, usesDiagnostics) = CollectUsesDependencies(symbol);
        foreach (var d in usesDiagnostics)
            diagnostics.Add(d);

        if (handlers.Count == 0 && lazyAccessors.Length == 0 && usesDeps.Length == 0 && diagnostics.Count == 0)
            return null;

        var containingTypes = ImmutableArray.CreateBuilder<string>();
        var outer = symbol.ContainingType;
        while (outer is not null)
        {
            containingTypes.Insert(0, outer.Name);
            outer = outer.ContainingType;
        }

        return new ModuleInfo(
            symbol.Name,
            GetNamespace(symbol),
            containingTypes.ToImmutable(),
            handlers.ToImmutable(),
            lazyAccessors,
            usesDeps,
            diagnostics.ToImmutable());
    }

    private static ImmutableArray<LazyAccessorInfo> CollectLazyAccessors(INamedTypeSymbol symbol)
    {
        IMethodSymbol? primaryCtor = null;
        foreach (var ctor in symbol.InstanceConstructors)
        {
            foreach (var declRef in ctor.DeclaringSyntaxReferences)
            {
                if (declRef.GetSyntax() is ClassDeclarationSyntax)
                {
                    primaryCtor = ctor;
                    break;
                }
            }
            if (primaryCtor is not null) break;
        }

        if (primaryCtor is null) return ImmutableArray<LazyAccessorInfo>.Empty;

        var builder = ImmutableArray.CreateBuilder<LazyAccessorInfo>();
        var existingMemberNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in symbol.GetMembers())
            existingMemberNames.Add(member.Name);

        foreach (var param in primaryCtor.Parameters)
        {
            if (param.Type is not INamedTypeSymbol paramType) continue;
            if (paramType.ConstructedFrom?.SpecialType != SpecialType.None) continue;
            if (paramType.Name != "Lazy" || paramType.TypeArguments.Length != 1) continue;
            if (paramType.TypeArguments[0] is not INamedTypeSymbol inner) continue;
            var paramName = param.Name;
            if (paramName.StartsWith("_")) continue;
            var propName = "_" + paramName;
            if (existingMemberNames.Contains(propName)) continue;

            builder.Add(new LazyAccessorInfo(
                ParameterName: paramName,
                PropertyName: propName,
                InnerTypeFullyQualified: ToFullyQualified(inner)));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Reads <c>[Uses&lt;TDependency&gt;]</c> attributes into the primary-constructor dependencies
    /// the generator will emit. Dependencies are de-duplicated and ordered by fully-qualified name
    /// so the emitted parameter list matches the argument order the parser generator produces from
    /// the same attributes (the two generators cannot see each other's output).
    /// <para>
    /// Any explicitly-declared constructor takes precedence: <c>[Uses&lt;&gt;]</c> is dropped with an
    /// FA0018 warning rather than emitting a second constructor. A dependency whose accessor name
    /// would collide with an existing member is likewise skipped.
    /// </para>
    /// </summary>
    private static (ImmutableArray<UsesDepInfo> Deps, ImmutableArray<PendingDiagnostic> Diagnostics) CollectUsesDependencies(INamedTypeSymbol symbol)
    {
        var depTypes = new List<INamedTypeSymbol>();
        foreach (var attr in symbol.GetAttributes())
        {
            if (!IsDependencyAttribute(attr.AttributeClass)) continue;
            if (attr.AttributeClass!.TypeArguments.Length != 1) continue;
            if (attr.AttributeClass.TypeArguments[0] is INamedTypeSymbol dep) depTypes.Add(dep);
        }

        if (depTypes.Count == 0)
            return (ImmutableArray<UsesDepInfo>.Empty, ImmutableArray<PendingDiagnostic>.Empty);

        var location = symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;
        var diagnostics = ImmutableArray.CreateBuilder<PendingDiagnostic>();

        var hasExplicitCtor = false;
        foreach (var ctor in symbol.InstanceConstructors)
        {
            if (!ctor.IsImplicitlyDeclared) { hasExplicitCtor = true; break; }
        }

        if (hasExplicitCtor)
        {
            foreach (var dep in depTypes)
                diagnostics.Add(new PendingDiagnostic(DependencyIgnoredDescriptor, location,
                    ImmutableArray.Create(dep.Name, symbol.Name, "a constructor is declared")));
            return (ImmutableArray<UsesDepInfo>.Empty, diagnostics.ToImmutable());
        }

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in symbol.GetMembers())
            usedNames.Add(member.Name);

        var deps = ImmutableArray.CreateBuilder<UsesDepInfo>();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dep in depTypes.OrderBy(ToFullyQualified, StringComparer.Ordinal))
        {
            var innerFq = ToFullyQualified(dep);
            if (!seenTypes.Add(innerFq)) continue;

            var propName = dep.Name;
            if (!usedNames.Add(propName))
            {
                diagnostics.Add(new PendingDiagnostic(DependencyIgnoredDescriptor, location,
                    ImmutableArray.Create(dep.Name, symbol.Name, $"a member named '{propName}' already exists")));
                continue;
            }

            deps.Add(new UsesDepInfo(innerFq, ToParameterName(dep.Name), propName));
        }

        return (deps.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool IsDependencyAttribute(INamedTypeSymbol? attrClass) =>
        attrClass is { IsGenericType: true }
        && attrClass.Name == DependencyAttributeFullName
        && attrClass.ContainingNamespace?.ToDisplayString() == AnalysisNamespace;

    private static string ToParameterName(string typeName)
    {
        var camel = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        return SyntaxFacts.GetKeywordKind(camel) == SyntaxKind.None ? camel : "@" + camel;
    }

    private static bool IsOnAttribute(AttributeData attr) =>
        attr.AttributeClass is { } cls
        && cls.Name == OnAttributeFullName
        && cls.ContainingNamespace?.ToDisplayString() == "FellowshipAnalyzer.Core.Analysis";

    private static HandlerInfo? BuildHandler(
        IMethodSymbol method,
        INamedTypeSymbol attrClass,
        INamedTypeSymbol eventType,
        AttributeData attr,
        SemanticModel semanticModel,
        ImmutableArray<PendingDiagnostic>.Builder diagnostics,
        CancellationToken ct)
    {
        if (method.Parameters.Length != 1)
            return null;

        var paramType = method.Parameters[0].Type;
        if (paramType is not INamedTypeSymbol paramNamed) return null;

        string? oneOfTypeFullyQualified = null;
        var oneOfSlotIndex = -1;
        if (HandlerSignatureRules.IsOneOfParam(paramNamed, out var oneOfType))
        {
            if (!HandlerSignatureRules.TryResolveOneOfSlot(oneOfType, eventType, out oneOfSlotIndex, out _))
                return null;
            oneOfTypeFullyQualified = ToFullyQualified(oneOfType);
        }
        else if (!HandlerSignatureRules.IsCompatibleParam(paramNamed, eventType))
        {
            return null;
        }

        var by = GetIntNamedArg(attr, "By");
        var to = GetIntNamedArg(attr, "To");

        var attrSyntax = attr.ApplicationSyntaxReference?.GetSyntax(ct) as AttributeSyntax;

        var spell = ResolveSpellNamedArg(attrSyntax, semanticModel, "Spell", diagnostics, ct);
        var spells = ResolveSpellArrayNamedArg(attrSyntax, semanticModel, "Spells", diagnostics, ct);
        var extraSpell = ResolveSpellNamedArg(attrSyntax, semanticModel, "ExtraSpell", diagnostics, ct);
        var extraSpells = ResolveSpellArrayNamedArg(attrSyntax, semanticModel, "ExtraSpells", diagnostics, ct);

        var isAsync = IsTaskReturning(method);
        var implementsAbility = ImplementsInterface(eventType, "IAbilityEvent");
        var implementsExtraAbility = ImplementsInterface(eventType, "IExtraAbilityEvent");
        var implementsHasSource = ImplementsInterface(eventType, "IHasSourceEvent");
        var implementsHasTarget = ImplementsInterface(eventType, "IHasTargetEvent");

        return new HandlerInfo(
            method.Name,
            ToFullyQualified(eventType),
            ByActor: by,
            ToActor: to,
            Spell: spell,
            Spells: spells,
            ExtraSpell: extraSpell,
            ExtraSpells: extraSpells,
            IsAsync: isAsync,
            EventImplementsAbility: implementsAbility,
            EventImplementsExtraAbility: implementsExtraAbility,
            EventImplementsHasSource: implementsHasSource,
            EventImplementsHasTarget: implementsHasTarget,
            OneOfTypeFullyQualified: oneOfTypeFullyQualified,
            OneOfSlotIndex: oneOfSlotIndex);
    }

    private static int GetIntNamedArg(AttributeData attr, string name)
    {
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == name && na.Value.Value is int i) return i;
        }
        return 0;
    }

    private static AttributeArgumentSyntax? FindNamedArgSyntax(AttributeSyntax? attrSyntax, string name)
    {
        if (attrSyntax?.ArgumentList is null) return null;
        foreach (var arg in attrSyntax.ArgumentList.Arguments)
        {
            if (arg.NameEquals is { } ne && ne.Name.Identifier.ValueText == name)
                return arg;
        }
        return null;
    }

    private static int? ResolveSpellNamedArg(
        AttributeSyntax? attrSyntax,
        SemanticModel semanticModel,
        string name,
        ImmutableArray<PendingDiagnostic>.Builder diagnostics,
        CancellationToken ct)
    {
        var argSyntax = FindNamedArgSyntax(attrSyntax, name);
        if (argSyntax is null) return null;
        return ResolveSpellExpression(argSyntax.Expression, semanticModel, name, diagnostics, ct);
    }

    private static ImmutableArray<int> ResolveSpellArrayNamedArg(
        AttributeSyntax? attrSyntax,
        SemanticModel semanticModel,
        string name,
        ImmutableArray<PendingDiagnostic>.Builder diagnostics,
        CancellationToken ct)
    {
        var argSyntax = FindNamedArgSyntax(attrSyntax, name);
        if (argSyntax is null) return ImmutableArray<int>.Empty;

        IEnumerable<ExpressionSyntax>? elements = argSyntax.Expression switch
        {
            ArrayCreationExpressionSyntax ac => ac.Initializer?.Expressions,
            ImplicitArrayCreationExpressionSyntax iac => iac.Initializer.Expressions,
            CollectionExpressionSyntax ce => ce.Elements.OfType<ExpressionElementSyntax>().Select(e => e.Expression),
            _ => null,
        };

        if (elements is null)
        {
            diagnostics.Add(new PendingDiagnostic(
                SpellArgMustBeNameOfDescriptor,
                argSyntax.Expression.GetLocation(),
                ImmutableArray.Create<string>(name)));
            return ImmutableArray<int>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<int>();
        foreach (var expr in elements)
        {
            var resolved = ResolveSpellExpression(expr, semanticModel, name, diagnostics, ct);
            if (resolved is int v) builder.Add(v);
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Resolves <c>nameof(Registry.Member)</c> to the registered spell's runtime FSLID
    /// (<see cref="FellowshipAnalyzer.Core.Common.Spells.Spell.FSLID"/>). Emits FA0002/3/4 on failure.
    /// </summary>
    private static int? ResolveSpellExpression(
        ExpressionSyntax expr,
        SemanticModel semanticModel,
        string argName,
        ImmutableArray<PendingDiagnostic>.Builder diagnostics,
        CancellationToken ct)
    {
        if (expr is not InvocationExpressionSyntax invocation ||
            invocation.Expression is not IdentifierNameSyntax id ||
            id.Identifier.ValueText != "nameof" ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            invocation.ArgumentList.Arguments[0].Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            diagnostics.Add(new PendingDiagnostic(
                SpellArgMustBeNameOfDescriptor,
                expr.GetLocation(),
                ImmutableArray.Create<string>(argName)));
            return null;
        }

        var symbol = semanticModel.GetSymbolInfo(memberAccess, ct).Symbol;
        if (symbol is not IPropertySymbol property ||
            !property.IsStatic ||
            !IsSpellOrEffect(property.Type) ||
            !ContainingTypeImplementsSpellRegistry(property))
        {
            diagnostics.Add(new PendingDiagnostic(
                SpellArgNotRegistryMemberDescriptor,
                memberAccess.GetLocation(),
                ImmutableArray.Create<string>(memberAccess.ToString())));
            return null;
        }

        if (!TryGetSpellFslid(property, ct, out var guid))
        {
            diagnostics.Add(new PendingDiagnostic(
                SpellArgInitializerUnresolvableDescriptor,
                memberAccess.GetLocation(),
                ImmutableArray.Create<string>(memberAccess.ToString())));
            return null;
        }

        return guid;
    }

    private static bool IsSpellOrEffect(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if ((current.Name == SpellTypeName || current.Name == EffectTypeName) &&
                current.ContainingNamespace?.ToDisplayString() == SpellsNamespace)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// True when the property's containing type is a spell registry — either it implements
    /// <c>ISpellRegistry</c> directly, or it is the static trigger class decorated with
    /// <c>[GenerateRegistry&lt;ISpellRegistry&gt;]</c>. The trigger class can't implement the
    /// interface itself (static classes can't have interfaces), but its own static
    /// <see cref="FellowshipAnalyzer.Core.Common.Spells.Spell"/> entries are included in the
    /// generated <c>All</c> dictionary.
    /// </summary>
    private static bool ContainingTypeImplementsSpellRegistry(IPropertySymbol property)
    {
        var owner = property.ContainingType;
        if (owner is null) return false;
        foreach (var iface in owner.AllInterfaces)
        {
            if (iface.Name == SpellRegistryInterfaceName &&
                iface.ContainingNamespace?.ToDisplayString() == SpellsNamespace)
                return true;
        }
        foreach (var attr in owner.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;
            if (cls.Name != "GenerateRegistryAttribute") continue;
            if (!cls.IsGenericType || cls.TypeArguments.Length != 1) continue;
            if (cls.TypeArguments[0] is INamedTypeSymbol arg &&
                arg.Name == SpellRegistryInterfaceName &&
                arg.ContainingNamespace?.ToDisplayString() == SpellsNamespace)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reads the spell's runtime FSLID for a registry property. Two paths, in order:
    /// <list type="bullet">
    /// <item>Metadata path — read the source-generated <c>[SpellId(&lt;fslid&gt;)]</c> attribute
    ///   stamped on the property. Works when the registry lives in a referenced assembly (the
    ///   typical hero-analyzer scenario).</item>
    /// <item>Syntax path — read the <c>Id =</c> member initializer as an int literal, then apply
    ///   the subtype range offset (<see cref="FellowshipAnalyzer.Core.Common.Spells.Effect"/> /
    ///   <c>Talent</c> / <c>Weapon</c>) by property type. Required for same-assembly references
    ///   because source generators do not see each other's output in the same compilation pass.</item>
    /// </list>
    /// </summary>
    private static bool TryGetSpellFslid(IPropertySymbol property, CancellationToken ct, out int fslid)
    {
        fslid = 0;

        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "SpellIdAttribute" &&
                attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is int value)
            {
                fslid = value;
                return true;
            }
        }

        if (property.DeclaringSyntaxReferences.Length == 0) return false;
        if (property.DeclaringSyntaxReferences[0].GetSyntax(ct) is not PropertyDeclarationSyntax pds) return false;
        if (pds.Initializer is not { } initializer) return false;

        if (TryReadInitializerId(initializer.Value, out var initId))
        {
            fslid = ApplyRangeOffset(property.Type, initId);
            return true;
        }

        return false;
    }

    private static bool TryReadIntLiteral(ExpressionSyntax expr, out int value)
    {
        if (expr is LiteralExpressionSyntax lit && lit.Token.Value is int li)
        {
            value = li;
            return true;
        }
        if (expr is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.UnaryMinusExpression) &&
            prefix.Operand is LiteralExpressionSyntax pl && pl.Token.Value is int pli)
        {
            value = -pli;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryReadInitializerId(ExpressionSyntax creation, out int id)
    {
        id = 0;
        InitializerExpressionSyntax? init = creation switch
        {
            ObjectCreationExpressionSyntax oc => oc.Initializer,
            ImplicitObjectCreationExpressionSyntax ioc => ioc.Initializer,
            _ => null,
        };
        if (init is null) return false;
        foreach (var expr in init.Expressions)
        {
            if (expr is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "Id" }, Right: { } rhs }
                && TryReadIntLiteral(rhs, out id))
                return true;
        }
        return false;
    }

    private static int ApplyRangeOffset(ITypeSymbol type, int id)
    {
        var current = type;
        while (current is not null)
        {
            if (current.ContainingNamespace?.ToDisplayString() == SpellsNamespace)
            {
                if (current.Name == "Effect") return 1_000_000 + id;
                if (current.Name == "Talent") return 2_000_000 + id;
                if (current.Name == "Weapon") return 3_000_000 + id;
            }
            current = current.BaseType;
        }
        return id;
    }

    private static ImmutableArray<int> GetIntArrayNamedArg(AttributeData attr, string name)
    {
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key != name) continue;
            if (na.Value.Kind != TypedConstantKind.Array) continue;
            var values = na.Value.Values;
            var builder = ImmutableArray.CreateBuilder<int>(values.Length);
            foreach (var v in values)
            {
                if (v.Value is int i) builder.Add(i);
            }
            return builder.ToImmutable();
        }
        return ImmutableArray<int>.Empty;
    }

    private static bool IsTaskReturning(IMethodSymbol method)
    {
        if (method.ReturnsVoid) return false;
        var rt = method.ReturnType;
        return rt.Name == "Task" || rt.Name == "ValueTask";
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == interfaceName) return true;
        }
        return false;
    }

    private static bool InheritsFromEventSubscriber(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current is not null)
        {
            if (current.Name == "EventSubscriber") return true;
            current = current.BaseType;
        }
        return false;
    }

    private static string GetNamespace(INamedTypeSymbol symbol) =>
        symbol.ContainingNamespace?.IsGlobalNamespace == false
            ? symbol.ContainingNamespace.ToDisplayString()
            : string.Empty;

    private static string ToFullyQualified(INamedTypeSymbol symbol) =>
        "global::" + symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));

    private static void Emit(SourceProductionContext ctx, ModuleInfo info)
    {
        foreach (var pending in info.Diagnostics)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                pending.Descriptor,
                pending.Location,
                pending.MessageArgs.ToArray()));
        }

        if (info.Handlers.Length == 0 && info.LazyAccessors.Length == 0 && info.UsesDeps.Length == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using global::FellowshipAnalyzer.Core.Analysis;");
        sb.AppendLine("using global::FellowshipAnalyzer.Core.Events;");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.Append("namespace ").Append(info.Namespace).AppendLine(";");
            sb.AppendLine();
        }

        var indent = string.Empty;
        foreach (var outer in info.ContainingTypes)
        {
            sb.Append(indent).Append("partial class ").AppendLine(outer);
            sb.Append(indent).AppendLine("{");
            indent += "    ";
        }

        if (info.UsesDeps.Length > 0)
        {
            sb.Append(indent).AppendLine("/// <summary>");
            sb.Append(indent).AppendLine("/// Constructs the module, resolving each <c>[Dependency&lt;T&gt;]</c> sibling module through the");
            sb.Append(indent).AppendLine("/// owning parser the first time its generated accessor is read.");
            sb.Append(indent).AppendLine("/// </summary>");
            foreach (var dep in info.UsesDeps)
            {
                sb.Append(indent).Append("/// <param name=\"").Append(dep.ParameterName)
                  .Append("\">The lazily-resolved <see cref=\"").Append(dep.InnerTypeFullyQualified.Substring("global::".Length))
                  .AppendLine("\"/> this module declared a dependency on.</param>");
            }
        }

        sb.Append(indent).Append("partial class ").Append(info.ClassName);
        if (info.UsesDeps.Length > 0)
        {
            sb.Append('(');
            for (var i = 0; i < info.UsesDeps.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("global::System.Lazy<").Append(info.UsesDeps[i].InnerTypeFullyQualified)
                  .Append("> ").Append(info.UsesDeps[i].ParameterName);
            }
            sb.Append(')');
        }
        sb.AppendLine();
        sb.Append(indent).AppendLine("{");

        var bodyIndent = indent + "    ";
        foreach (var accessor in info.LazyAccessors)
        {
            sb.Append(bodyIndent).Append("private ").Append(accessor.InnerTypeFullyQualified)
              .Append(' ').Append(accessor.PropertyName)
              .Append(" => field ??= ").Append(accessor.ParameterName).AppendLine(".Value;");
        }
        foreach (var dep in info.UsesDeps)
        {
            sb.Append(bodyIndent).Append("private ").Append(dep.InnerTypeFullyQualified)
              .Append(' ').Append(dep.PropertyName)
              .Append(" => field ??= ").Append(dep.ParameterName).AppendLine(".Value;");
        }
        if ((info.LazyAccessors.Length > 0 || info.UsesDeps.Length > 0) && info.Handlers.Length > 0)
            sb.AppendLine();

        if (info.Handlers.Length > 0)
        {
            sb.Append(bodyIndent).AppendLine("/// <inheritdoc/>");
            sb.Append(bodyIndent).AppendLine("protected override void RegisterAttributeSubscriptions()");
            sb.Append(bodyIndent).AppendLine("{");
            sb.Append(bodyIndent).AppendLine("    base.RegisterAttributeSubscriptions();");
            sb.Append(bodyIndent).AppendLine("    var __owner = Owner;");
            sb.Append(bodyIndent).AppendLine("    var __emitter = __owner.EventEmitter;");

            var index = 0;
            foreach (var h in info.Handlers)
            {
                EmitHandler(sb, h, index++, bodyIndent + "    ");
            }

            sb.Append(bodyIndent).AppendLine("}");
        }

        sb.Append(indent).AppendLine("}");

        for (var i = info.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.Append(indent).AppendLine("}");
        }

        var fileName = info.ContainingTypes.Length == 0
            ? info.ClassName + ".Module.g.cs"
            : string.Join("+", info.ContainingTypes) + "+" + info.ClassName + ".Module.g.cs";
        ctx.AddSource(fileName, sb.ToString());
    }

    private static void EmitHandler(StringBuilder sb, HandlerInfo h, int index, string indent)
    {
        var local = "__e" + index;
        var conditions = new List<string>();

        var byHasSelf = (h.ByActor & 1) != 0;
        var byHasPet = (h.ByActor & 2) != 0;
        if (byHasSelf && h.EventImplementsHasSource)
            conditions.Add("__owner.ByPlayer(" + local + ", null)");
        if (byHasPet && h.EventImplementsHasSource)
            conditions.Add("__owner.ByPlayerPet(" + local + ")");
        if (byHasSelf && byHasPet && h.EventImplementsHasSource)
        {
            var pet = conditions[conditions.Count - 1];
            var self = conditions[conditions.Count - 2];
            conditions.RemoveAt(conditions.Count - 1);
            conditions.RemoveAt(conditions.Count - 1);
            conditions.Add("(" + self + " || " + pet + ")");
        }

        var toHasSelf = (h.ToActor & 1) != 0;
        var toHasPet = (h.ToActor & 2) != 0;
        if (toHasSelf && h.EventImplementsHasTarget)
            conditions.Add("__owner.ToPlayer(" + local + ", null)");
        if (toHasPet && h.EventImplementsHasTarget)
            conditions.Add("__owner.ToPlayerPet(" + local + ")");
        if (toHasSelf && toHasPet && h.EventImplementsHasTarget)
        {
            var pet = conditions[conditions.Count - 1];
            var self = conditions[conditions.Count - 2];
            conditions.RemoveAt(conditions.Count - 1);
            conditions.RemoveAt(conditions.Count - 1);
            conditions.Add("(" + self + " || " + pet + ")");
        }

        if (h.EventImplementsAbility)
        {
            if (h.Spell is int s)
                conditions.Add(local + ".Ability.Id == " + s);
            else if (h.Spells.Length > 0)
                conditions.Add(BuildIdInList(local + ".Ability.Id", h.Spells));
        }
        if (h.EventImplementsExtraAbility)
        {
            if (h.ExtraSpell is int es)
                conditions.Add(local + ".ExtraAbility.Id == " + es);
            else if (h.ExtraSpells.Length > 0)
                conditions.Add(BuildIdInList(local + ".ExtraAbility.Id", h.ExtraSpells));
        }

        var predicate = conditions.Count == 0
            ? "e is " + h.EventTypeFullyQualified + " " + local
            : "e is " + h.EventTypeFullyQualified + " " + local + " && " + string.Join(" && ", conditions);

        string argExpression;
        if (h.OneOfTypeFullyQualified is not null)
        {
            argExpression = h.OneOfTypeFullyQualified + ".FromT" + h.OneOfSlotIndex
                + "((" + h.EventTypeFullyQualified + ")e)";
        }
        else
        {
            argExpression = "(" + h.EventTypeFullyQualified + ")e";
        }

        sb.Append(indent).Append("__emitter.Subscribe(this, ");
        sb.Append("(global::System.Func<global::FellowshipAnalyzer.Core.Events.Event, bool>)(e => ").Append(predicate).Append("), ");
        if (h.IsAsync)
            sb.Append("(global::System.Func<global::FellowshipAnalyzer.Core.Events.Event, global::System.Threading.Tasks.Task>)(e => ")
              .Append(h.MethodName).Append("(").Append(argExpression).Append("))");
        else
            sb.Append("(global::System.Action<global::FellowshipAnalyzer.Core.Events.Event>)(e => ")
              .Append(h.MethodName).Append("(").Append(argExpression).Append("))");
        sb.AppendLine(");");
    }

    private static string BuildIdInList(string accessor, ImmutableArray<int> ids)
    {
        var parts = new List<string>(ids.Length);
        foreach (var id in ids)
            parts.Add(accessor + " == " + id);
        return "(" + string.Join(" || ", parts) + ")";
    }

    private sealed class ModuleInfo
    {
        public ModuleInfo(
            string className,
            string ns,
            ImmutableArray<string> containingTypes,
            ImmutableArray<HandlerInfo> handlers,
            ImmutableArray<LazyAccessorInfo> lazyAccessors,
            ImmutableArray<UsesDepInfo> usesDeps,
            ImmutableArray<PendingDiagnostic> diagnostics)
        {
            ClassName = className;
            Namespace = ns;
            ContainingTypes = containingTypes;
            Handlers = handlers;
            LazyAccessors = lazyAccessors;
            UsesDeps = usesDeps;
            Diagnostics = diagnostics;
        }
        public string ClassName { get; }
        public string Namespace { get; }
        public ImmutableArray<string> ContainingTypes { get; }
        public ImmutableArray<HandlerInfo> Handlers { get; }
        public ImmutableArray<LazyAccessorInfo> LazyAccessors { get; }
        public ImmutableArray<UsesDepInfo> UsesDeps { get; }
        public ImmutableArray<PendingDiagnostic> Diagnostics { get; }
    }

    private sealed class HandlerInfo
    {
        public HandlerInfo(
            string methodName,
            string eventTypeFullyQualified,
            int ByActor,
            int ToActor,
            int? Spell,
            ImmutableArray<int> Spells,
            int? ExtraSpell,
            ImmutableArray<int> ExtraSpells,
            bool IsAsync,
            bool EventImplementsAbility,
            bool EventImplementsExtraAbility,
            bool EventImplementsHasSource,
            bool EventImplementsHasTarget,
            string? OneOfTypeFullyQualified,
            int OneOfSlotIndex)
        {
            MethodName = methodName;
            EventTypeFullyQualified = eventTypeFullyQualified;
            this.ByActor = ByActor;
            this.ToActor = ToActor;
            this.Spell = Spell;
            this.Spells = Spells;
            this.ExtraSpell = ExtraSpell;
            this.ExtraSpells = ExtraSpells;
            this.IsAsync = IsAsync;
            this.EventImplementsAbility = EventImplementsAbility;
            this.EventImplementsExtraAbility = EventImplementsExtraAbility;
            this.EventImplementsHasSource = EventImplementsHasSource;
            this.EventImplementsHasTarget = EventImplementsHasTarget;
            this.OneOfTypeFullyQualified = OneOfTypeFullyQualified;
            this.OneOfSlotIndex = OneOfSlotIndex;
        }
        public string MethodName { get; }
        public string EventTypeFullyQualified { get; }
        public int ByActor { get; }
        public int ToActor { get; }
        public int? Spell { get; }
        public ImmutableArray<int> Spells { get; }
        public int? ExtraSpell { get; }
        public ImmutableArray<int> ExtraSpells { get; }
        public bool IsAsync { get; }
        public bool EventImplementsAbility { get; }
        public bool EventImplementsExtraAbility { get; }
        public bool EventImplementsHasSource { get; }
        public bool EventImplementsHasTarget { get; }
        public string? OneOfTypeFullyQualified { get; }
        public int OneOfSlotIndex { get; }
    }

    private sealed record LazyAccessorInfo(
        string ParameterName,
        string PropertyName,
        string InnerTypeFullyQualified);

    private sealed record UsesDepInfo(
        string InnerTypeFullyQualified,
        string ParameterName,
        string PropertyName);

    private sealed record PendingDiagnostic(
        DiagnosticDescriptor Descriptor,
        Location? Location,
        ImmutableArray<string> MessageArgs);
}
