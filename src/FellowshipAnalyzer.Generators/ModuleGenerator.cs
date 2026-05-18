using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Emits the per-module generated partial: a <c>RegisterAttributeSubscriptions</c> override
/// for any <see cref="OnAttribute{TEvent}"/> handlers declared on the class, and a cached
/// private accessor for every primary-constructor parameter of type <c>Lazy&lt;TModule&gt;</c>.
/// One hand-written module file produces exactly one generated partial file.
/// </summary>
[Generator]
public sealed class ModuleGenerator : IIncrementalGenerator
{
    private const string OnAttributeShortName = "On";
    private const string OnAttributeFullName = "OnAttribute";

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

        // Candidate if the class either declares an [On<>] handler OR has any
        // primary-ctor parameter (we'll filter by Lazy<T> later in the semantic pass).
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

        // For partial classes spanning multiple files, emit only when visiting the
        // syntax tree that contains the first declaration (sorted by file path).
        // This keeps the generator output deterministic and avoids duplicate overrides.
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
            if (!string.Equals(currentPath, earliestPath, System.StringComparison.Ordinal))
                return null;
        }

        var inheritsEventSubscriber = InheritsFromEventSubscriber(symbol);

        var handlers = ImmutableArray.CreateBuilder<HandlerInfo>();
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

                    var handler = BuildHandler(method, attrClass, eventType, attrData);
                    if (handler is not null)
                        handlers.Add(handler);
                }
            }
        }

        var lazyAccessors = CollectLazyAccessors(symbol);

        if (handlers.Count == 0 && lazyAccessors.Length == 0)
            return null;

        var hasEventSubscriberBaseWithAttributes = inheritsEventSubscriber && AnyBaseHasOnAttributes(symbol);

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
            hasEventSubscriberBaseWithAttributes);
    }

    private static ImmutableArray<LazyAccessorInfo> CollectLazyAccessors(INamedTypeSymbol symbol)
    {
        // Primary-constructor parameters are exposed as members of the type via the symbol
        // model only indirectly. Locate the primary constructor by checking for the symbol's
        // associated InstanceConstructors whose declaring syntax is the class itself.
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
        var existingMemberNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var member in symbol.GetMembers())
            existingMemberNames.Add(member.Name);

        foreach (var param in primaryCtor.Parameters)
        {
            if (param.Type is not INamedTypeSymbol paramType) continue;
            if (paramType.ConstructedFrom?.SpecialType != SpecialType.None) continue;
            if (paramType.Name != "Lazy" || paramType.TypeArguments.Length != 1) continue;
            if (paramType.TypeArguments[0] is not INamedTypeSymbol inner) continue;
            // Skip if the parameter name already begins with an underscore — the caller
            // already controls the surface and we'd collide on the generated property.
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

    private static bool AnyBaseHasOnAttributes(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Name == "EventSubscriber" || current.Name == "Module") break;
            foreach (var member in current.GetMembers())
            {
                if (member is not IMethodSymbol method) continue;
                foreach (var attr in method.GetAttributes())
                {
                    if (IsOnAttribute(attr)) return true;
                }
            }
            current = current.BaseType;
        }
        return false;
    }

    private static bool IsOnAttribute(AttributeData attr) =>
        attr.AttributeClass is { } cls
        && cls.Name == OnAttributeFullName
        && cls.ContainingNamespace?.ToDisplayString() == "FellowshipAnalyzer.Core.Analysis";

    private static HandlerInfo? BuildHandler(
        IMethodSymbol method,
        INamedTypeSymbol attrClass,
        INamedTypeSymbol eventType,
        AttributeData attr)
    {
        if (method.Parameters.Length != 1)
            return null;

        var paramType = method.Parameters[0].Type;
        if (paramType is not INamedTypeSymbol paramNamed) return null;
        if (!SymbolEqualityComparer.Default.Equals(paramNamed, eventType)
            && !InheritsFrom(eventType, paramNamed))
            return null;

        var by = GetIntNamedArg(attr, "By");
        var to = GetIntNamedArg(attr, "To");
        var spell = GetIntNamedArg(attr, "Spell");
        var spells = GetIntArrayNamedArg(attr, "Spells");
        var extraSpell = GetIntNamedArg(attr, "ExtraSpell");
        var extraSpells = GetIntArrayNamedArg(attr, "ExtraSpells");

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
            EventImplementsHasTarget: implementsHasTarget);
    }

    private static int GetIntNamedArg(AttributeData attr, string name)
    {
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == name && na.Value.Value is int i) return i;
        }
        return 0;
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

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            current = current.BaseType;
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

        sb.Append(indent).Append("partial class ").AppendLine(info.ClassName);
        sb.Append(indent).AppendLine("{");

        var bodyIndent = indent + "    ";
        foreach (var accessor in info.LazyAccessors)
        {
            sb.Append(bodyIndent).Append("private ").Append(accessor.InnerTypeFullyQualified)
              .Append(' ').Append(accessor.PropertyName)
              .Append(" => field ??= ").Append(accessor.ParameterName).AppendLine(".Value;");
        }
        if (info.LazyAccessors.Length > 0 && info.Handlers.Length > 0)
            sb.AppendLine();

        if (info.Handlers.Length > 0)
        {
            sb.Append(bodyIndent).AppendLine("protected override void RegisterAttributeSubscriptions()");
            sb.Append(bodyIndent).AppendLine("{");
            if (info.BaseHasAttributeHandlers)
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
            if (h.Spell != 0)
                conditions.Add(local + ".Ability.Id == " + h.Spell);
            else if (h.Spells.Length > 0)
                conditions.Add(BuildIdInList(local + ".Ability.Id", h.Spells));
        }
        if (h.EventImplementsExtraAbility)
        {
            if (h.ExtraSpell != 0)
                conditions.Add(local + ".ExtraAbility.Id == " + h.ExtraSpell);
            else if (h.ExtraSpells.Length > 0)
                conditions.Add(BuildIdInList(local + ".ExtraAbility.Id", h.ExtraSpells));
        }

        var predicate = conditions.Count == 0
            ? "e is " + h.EventTypeFullyQualified + " " + local
            : "e is " + h.EventTypeFullyQualified + " " + local + " && " + string.Join(" && ", conditions);

        sb.Append(indent).Append("__emitter.Subscribe(this, ");
        sb.Append("(global::System.Func<global::FellowshipAnalyzer.Core.Events.Event, bool>)(e => ").Append(predicate).Append("), ");
        if (h.IsAsync)
            sb.Append("(global::System.Func<global::FellowshipAnalyzer.Core.Events.Event, global::System.Threading.Tasks.Task>)(e => ")
              .Append(h.MethodName).Append("((").Append(h.EventTypeFullyQualified).Append(")e))");
        else
            sb.Append("(global::System.Action<global::FellowshipAnalyzer.Core.Events.Event>)(e => ")
              .Append(h.MethodName).Append("((").Append(h.EventTypeFullyQualified).Append(")e))");
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
            bool baseHasAttributeHandlers)
        {
            ClassName = className;
            Namespace = ns;
            ContainingTypes = containingTypes;
            Handlers = handlers;
            LazyAccessors = lazyAccessors;
            BaseHasAttributeHandlers = baseHasAttributeHandlers;
        }
        public string ClassName { get; }
        public string Namespace { get; }
        public ImmutableArray<string> ContainingTypes { get; }
        public ImmutableArray<HandlerInfo> Handlers { get; }
        public ImmutableArray<LazyAccessorInfo> LazyAccessors { get; }
        public bool BaseHasAttributeHandlers { get; }
    }

    private sealed class HandlerInfo
    {
        public HandlerInfo(
            string methodName,
            string eventTypeFullyQualified,
            int ByActor,
            int ToActor,
            int Spell,
            ImmutableArray<int> Spells,
            int ExtraSpell,
            ImmutableArray<int> ExtraSpells,
            bool IsAsync,
            bool EventImplementsAbility,
            bool EventImplementsExtraAbility,
            bool EventImplementsHasSource,
            bool EventImplementsHasTarget)
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
        }
        public string MethodName { get; }
        public string EventTypeFullyQualified { get; }
        public int ByActor { get; }
        public int ToActor { get; }
        public int Spell { get; }
        public ImmutableArray<int> Spells { get; }
        public int ExtraSpell { get; }
        public ImmutableArray<int> ExtraSpells { get; }
        public bool IsAsync { get; }
        public bool EventImplementsAbility { get; }
        public bool EventImplementsExtraAbility { get; }
        public bool EventImplementsHasSource { get; }
        public bool EventImplementsHasTarget { get; }
    }

    private sealed record LazyAccessorInfo(
        string ParameterName,
        string PropertyName,
        string InnerTypeFullyQualified);
}
