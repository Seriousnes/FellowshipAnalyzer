using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Emits <c>RegisterAttributeSubscriptions</c> overrides for classes that declare
/// <see cref="OnAttribute{TEvent}"/> handlers. Each handler is wired directly into the
/// <c>EventEmitter</c> with an inlined predicate — no <c>Expression.Compile()</c> at runtime,
/// no LINQ tree allocation, and no per-analysis subscription cost. Implements the §1
/// proposal from the FellowshipAnalyzer redesign doc.
/// </summary>
[Generator]
public sealed class EventSubscriptionGenerator : IIncrementalGenerator
{
    private const string OnAttributeShortName = "On";
    private const string OnAttributeFullName = "OnAttribute";
    private const string EventNamespace = "FellowshipAnalyzer.Core.Events";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var subscribers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => GetSubscriberInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        context.RegisterSourceOutput(subscribers, Emit);
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

    private static SubscriberInfo? GetSubscriberInfo(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        // Class must derive from EventSubscriber to get a virtual hook to override.
        if (!InheritsFromEventSubscriber(symbol))
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

        var handlers = ImmutableArray.CreateBuilder<HandlerInfo>();
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

        if (handlers.Count == 0)
            return null;

        var hasEventSubscriberBaseWithAttributes = AnyBaseHasOnAttributes(symbol);

        return new SubscriberInfo(
            symbol.Name,
            GetNamespace(symbol),
            handlers.ToImmutable(),
            hasEventSubscriberBaseWithAttributes);
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
        // Handler parameter type must be assignable from the attribute's TEvent.
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

    private static void Emit(SourceProductionContext ctx, SubscriberInfo info)
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

        sb.Append("partial class ").AppendLine(info.ClassName);
        sb.AppendLine("{");
        sb.AppendLine("    protected override void RegisterAttributeSubscriptions()");
        sb.AppendLine("    {");
        if (info.BaseHasAttributeHandlers)
            sb.AppendLine("        base.RegisterAttributeSubscriptions();");

        sb.AppendLine("        var __owner = Owner;");
        sb.AppendLine("        var __emitter = __owner.EventEmitter;");

        var index = 0;
        foreach (var h in info.Handlers)
        {
            EmitHandler(sb, h, index++);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        ctx.AddSource(info.ClassName + ".OnHandlers.g.cs", sb.ToString());
    }

    private static void EmitHandler(StringBuilder sb, HandlerInfo h, int index)
    {
        var local = "__e" + index;
        var conditions = new List<string>();

        // Source actor checks.
        var byHasSelf = (h.ByActor & 1) != 0;
        var byHasPet = (h.ByActor & 2) != 0;
        if (byHasSelf && h.EventImplementsHasSource)
            conditions.Add("__owner.ByPlayer(" + local + ", null)");
        if (byHasPet && h.EventImplementsHasSource)
            conditions.Add("__owner.ByPlayerPet(" + local + ")");
        if (byHasSelf && byHasPet && h.EventImplementsHasSource)
        {
            // OR-combined: rewrite the last two as an OR.
            var pet = conditions[conditions.Count - 1];
            var self = conditions[conditions.Count - 2];
            conditions.RemoveAt(conditions.Count - 1);
            conditions.RemoveAt(conditions.Count - 1);
            conditions.Add("(" + self + " || " + pet + ")");
        }

        // Target actor checks.
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

        // Ability filters.
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

        sb.Append("        __emitter.Subscribe(this, ");
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

    private sealed class SubscriberInfo
    {
        public SubscriberInfo(
            string className,
            string ns,
            ImmutableArray<HandlerInfo> handlers,
            bool baseHasAttributeHandlers)
        {
            ClassName = className;
            Namespace = ns;
            Handlers = handlers;
            BaseHasAttributeHandlers = baseHasAttributeHandlers;
        }
        public string ClassName { get; }
        public string Namespace { get; }
        public ImmutableArray<HandlerInfo> Handlers { get; }
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
}
