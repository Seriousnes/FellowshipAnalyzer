using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Text;

namespace FellowshipAnalyzer.Generators;

[Generator]
public sealed class RegistryGenerator : IIncrementalGenerator
{
    private const string AttributeName = "GenerateRegistryAttribute";
    private const string SpellsNamespace = "FellowshipAnalyzer.Core.Common.Spells";
    private const string EffectTypeName = "Effect";

    private static readonly DiagnosticDescriptor DuplicateEntryDescriptor = new(
        id: "FA0001",
        title: "Duplicate property name in registry",
        messageFormat: "Property '{0}' is defined in both '{1}' and '{2}'. Rename one to resolve the conflict.",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var triggerClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsPartialStaticClassWithAttributes(node),
                transform: static (ctx, ct) => GetTriggerInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        context.RegisterSourceOutput(triggerClasses, Execute);
    }

    private static bool IsPartialStaticClassWithAttributes(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        if (classDecl.AttributeLists.Count == 0)
            return false;

        bool isPartial = false;
        bool isStatic = false;
        foreach (var modifier in classDecl.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword)) isPartial = true;
            if (modifier.IsKind(SyntaxKind.StaticKeyword)) isStatic = true;
        }

        return isPartial && isStatic;
    }

    private static TriggerInfo? GetTriggerInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        INamedTypeSymbol? registryInterface = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == AttributeName &&
                attr.AttributeClass.IsGenericType &&
                attr.AttributeClass.TypeArguments.Length == 1)
            {
                registryInterface = attr.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
        }

        if (registryInterface == null)
            return null;

        var registries = new List<RegistryInfo>();
        ITypeSymbol? lcaType = null;

        foreach (var type in GetAllNamedTypes(ctx.SemanticModel.Compilation.Assembly.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();

            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                continue;

            if (!ImplementsInterface(type, registryInterface))
                continue;

            var entries = new List<EntryProperty>();
            foreach (var member in type.GetMembers())
            {
                if (member is not IPropertySymbol prop)
                    continue;
                if (!prop.IsStatic || prop.GetMethod == null)
                    continue;
                if (!HasGuidProperty(prop.Type))
                    continue;

                if (!TryComputeGuid(prop, ct, out var guid))
                    continue;

                entries.Add(new EntryProperty(prop.Name, GlobalName(prop.Type), guid));
                lcaType = lcaType == null ? prop.Type : ComputeLca(lcaType, prop.Type);
            }

            if (entries.Count == 0)
                continue;

            registries.Add(new RegistryInfo(type.Name, NamespaceOf(type), [.. entries]));
        }

        registries.Sort(static (a, b) => string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal));

        var ownEntries = new List<EntryProperty>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;
            if (!prop.IsStatic || prop.GetMethod == null)
                continue;
            if (!HasGuidProperty(prop.Type))
                continue;
            if (!TryComputeGuid(prop, ct, out var guid))
                continue;

            ownEntries.Add(new EntryProperty(prop.Name, GlobalName(prop.Type), guid));
            lcaType = lcaType == null ? prop.Type : ComputeLca(lcaType, prop.Type);
        }

        var lcaGlobalName = lcaType != null ? GlobalName(lcaType) : null;

        return new TriggerInfo(
            symbol.Name,
            NamespaceOf(symbol),
            [.. registries],
            [.. ownEntries],
            lcaGlobalName);
    }

    /// <summary>
    /// Reads the int from the property initializer's constructor (e.g. <c>new(1318, ...)</c>),
    /// then applies the Effect encoding rule when the property type derives from <c>Effect</c>.
    /// </summary>
    private static bool TryComputeGuid(IPropertySymbol property, CancellationToken ct, out int guid)
    {
        guid = 0;
        if (!TryReadCtorInt(property, ct, out var id)) return false;
        guid = InheritsFromEffect(property.Type) ? 1_000_000 + id : id;
        return true;
    }

    private static bool TryReadCtorInt(IPropertySymbol property, CancellationToken ct, out int id)
    {
        id = 0;
        if (property.DeclaringSyntaxReferences.Length == 0) return false;
        if (property.DeclaringSyntaxReferences[0].GetSyntax(ct) is not PropertyDeclarationSyntax pds) return false;
        if (pds.Initializer is not { } initializer) return false;

        if (initializer.Value is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
        {
            var initExpr = initializer.Value switch
            {
                ObjectCreationExpressionSyntax o => o.Initializer,
                ImplicitObjectCreationExpressionSyntax io => io.Initializer,
                _ => null,
            };
            if (initExpr is not null)
                foreach (var e in initExpr.Expressions)
                    if (e is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "Id" } } a
                        && a.Right is LiteralExpressionSyntax { Token.Value: int initId })
                    { id = initId; return true; }
        }

        ArgumentListSyntax? argList = initializer.Value switch
        {
            ObjectCreationExpressionSyntax oc => oc.ArgumentList,
            ImplicitObjectCreationExpressionSyntax ioc => ioc.ArgumentList,
            _ => null,
        };
        if (argList is null || argList.Arguments.Count == 0) return false;

        var firstExpr = argList.Arguments[0].Expression;
        if (firstExpr is LiteralExpressionSyntax lit && lit.Token.Value is int li)
        {
            id = li;
            return true;
        }
        if (firstExpr is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.UnaryMinusExpression) &&
            prefix.Operand is LiteralExpressionSyntax pl && pl.Token.Value is int pli)
        {
            id = -pli;
            return true;
        }
        return false;
    }

    private static bool InheritsFromEffect(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (current.Name == EffectTypeName &&
                current.ContainingNamespace?.ToDisplayString() == SpellsNamespace)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static void Execute(SourceProductionContext ctx, TriggerInfo trigger)
    {
        if (trigger.Registries.IsEmpty && trigger.OwnEntries.IsEmpty)
            return;

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        bool hasError = false;
        foreach (var registry in trigger.Registries)
        {
            foreach (var entry in registry.Entries)
            {
                if (seen.TryGetValue(entry.PropertyName, out var existing))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        DuplicateEntryDescriptor,
                        Location.None,
                        entry.PropertyName,
                        existing,
                        registry.TypeName));
                    hasError = true;
                }
                else
                {
                    seen[entry.PropertyName] = registry.TypeName;
                }
            }
        }

        if (hasError)
            return;

        var triggerGlobalName = "global::" + (string.IsNullOrEmpty(trigger.Namespace)
            ? trigger.ClassName
            : trigger.Namespace + "." + trigger.ClassName);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace " + trigger.Namespace + ";");
        sb.AppendLine();
        sb.AppendLine("public static partial class " + trigger.ClassName);
        sb.AppendLine("{");

        bool firstBlock = true;
        foreach (var registry in trigger.Registries)
        {
            if (registry.GlobalName == triggerGlobalName)
                continue;

            if (!firstBlock) sb.AppendLine();
            firstBlock = false;

            sb.AppendLine("    // From " + registry.TypeName);
            foreach (var entry in registry.Entries)
            {
                sb.AppendLine("    public static " + entry.GlobalTypeName + " " + entry.PropertyName +
                              " => " + registry.GlobalName + "." + entry.PropertyName + ";");
            }
        }

        if (trigger.LcaGlobalName != null)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>All registered entries keyed by <c>Guid</c>.</summary>");
            sb.AppendLine("    public static FrozenDictionary<int, " + trigger.LcaGlobalName + "> All { get; } =");
            sb.AppendLine("        new Dictionary<int, " + trigger.LcaGlobalName + ">");
            sb.AppendLine("        {");
            foreach (var entry in trigger.OwnEntries)
            {
                sb.AppendLine("            [" + triggerGlobalName + "." + entry.PropertyName + ".Guid] = " +
                              triggerGlobalName + "." + entry.PropertyName + ",");
            }
            foreach (var registry in trigger.Registries)
            {
                foreach (var entry in registry.Entries)
                {
                    sb.AppendLine("            [" + registry.GlobalName + "." + entry.PropertyName + ".Guid] = " +
                                  registry.GlobalName + "." + entry.PropertyName + ",");
                }
            }
            sb.AppendLine("        }.ToFrozenDictionary();");
        }

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Compile-time int Guid constants for every registered spell. Used by the");
        sb.AppendLine("    /// <c>ModuleGenerator</c> to resolve <c>nameof(Registry.Member)</c> in");
        sb.AppendLine("    /// <c>[On&lt;...&gt;]</c> attribute arguments into <c>Ability.Id == &lt;guid&gt;</c>");
        sb.AppendLine("    /// predicates at codegen time. Includes entries from every discovered");
        sb.AppendLine("    /// <c>ISpellRegistry</c> implementor plus this trigger class's own entries.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class Guids");
        sb.AppendLine("    {");
        foreach (var entry in trigger.OwnEntries)
            sb.AppendLine("        public const int " + entry.PropertyName + " = " + entry.Guid + ";");
        foreach (var registry in trigger.Registries)
        {
            foreach (var entry in registry.Entries)
                sb.AppendLine("        public const int " + entry.PropertyName + " = " + entry.Guid + ";");
        }
        sb.AppendLine("    }");

        sb.AppendLine("}");

        ctx.AddSource(trigger.ClassName + ".g.cs", sb.ToString());

        EmitPerRegistryGuids(ctx, trigger.Registries, triggerGlobalName);
    }

    /// <summary>
    /// Emits a partial extension for each ISpellRegistry-implementing class with a nested
    /// static <c>Guids</c> class. Cross-assembly consumers (hero analyzers in separate projects)
    /// can read these via metadata since <c>const int</c> values are preserved in compiled assemblies.
    /// </summary>
    private static void EmitPerRegistryGuids(SourceProductionContext ctx, ImmutableArray<RegistryInfo> registries, string triggerGlobalName)
    {
        foreach (var registry in registries)
        {
            if (registry.GlobalName == triggerGlobalName)
                continue;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(registry.Namespace))
            {
                sb.AppendLine("namespace " + registry.Namespace + ";");
                sb.AppendLine();
            }
            sb.AppendLine("public partial class " + registry.TypeName);
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Compile-time int Guid constants for this registry's entries. Consumed by");
            sb.AppendLine("    /// <c>ModuleGenerator</c> when resolving <c>nameof(" + registry.TypeName + ".Member)</c>");
            sb.AppendLine("    /// in <c>[On&lt;...&gt;]</c> attributes.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class Guids");
            sb.AppendLine("    {");
            foreach (var entry in registry.Entries)
                sb.AppendLine("        public const int " + entry.PropertyName + " = " + entry.Guid + ";");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            var fileName = (string.IsNullOrEmpty(registry.Namespace)
                ? registry.TypeName
                : registry.Namespace.Replace('.', '_') + "_" + registry.TypeName) + ".Guids.g.cs";
            ctx.AddSource(fileName, sb.ToString());
        }
    }

    /// <summary>Returns true when <paramref name="type"/> directly or transitively implements <paramref name="targetInterface"/>.</summary>
    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol targetInterface)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, targetInterface))
                return true;
        }
        return false;
    }

    /// <summary>Returns true when <paramref name="type"/> or any of its base types has an <c>int Guid { get; }</c> property.</summary>
    private static bool HasGuidProperty(ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            foreach (var member in current.GetMembers("Guid"))
            {
                if (member is IPropertySymbol prop &&
                    prop.Type.SpecialType == SpecialType.System_Int32 &&
                    prop.GetMethod != null)
                    return true;
            }
            current = (current as INamedTypeSymbol)?.BaseType;
        }
        return false;
    }

    /// <summary>Computes the lowest common ancestor of two types by walking base-type chains.</summary>
    private static ITypeSymbol ComputeLca(ITypeSymbol a, ITypeSymbol b)
    {
        var ancestors = new HashSet<string>(StringComparer.Ordinal);
        var curr = a;
        while (curr != null)
        {
            ancestors.Add(curr.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            curr = (curr as INamedTypeSymbol)?.BaseType;
        }

        curr = b;
        while (curr != null)
        {
            if (ancestors.Contains(curr.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                return curr;
            curr = (curr as INamedTypeSymbol)?.BaseType;
        }

        return a;
    }

    private static string GlobalName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace?.IsGlobalNamespace == false
            ? type.ContainingNamespace.ToDisplayString()
            : string.Empty;

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetAllNestedTypes(type))
                yield return nested;
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in GetAllNamedTypes(childNs))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deepNested in GetAllNestedTypes(nested))
                yield return deepNested;
        }
    }

    private sealed class TriggerInfo
    {
        public TriggerInfo(
            string className,
            string ns,
            ImmutableArray<RegistryInfo> registries,
            ImmutableArray<EntryProperty> ownEntries,
            string? lcaGlobalName)
        {
            ClassName = className;
            Namespace = ns;
            Registries = registries;
            OwnEntries = ownEntries;
            LcaGlobalName = lcaGlobalName;
        }

        public string ClassName { get; }
        public string Namespace { get; }
        public ImmutableArray<RegistryInfo> Registries { get; }
        public ImmutableArray<EntryProperty> OwnEntries { get; }
        public string? LcaGlobalName { get; }
    }

    private sealed class RegistryInfo
    {
        public RegistryInfo(string typeName, string ns, ImmutableArray<EntryProperty> entries)
        {
            TypeName = typeName;
            Namespace = ns;
            Entries = entries;
            GlobalName = "global::" + (string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName);
        }

        public string TypeName { get; }
        public string Namespace { get; }
        public string GlobalName { get; }
        public ImmutableArray<EntryProperty> Entries { get; }
    }

    private sealed class EntryProperty
    {
        public EntryProperty(string propertyName, string globalTypeName, int guid)
        {
            PropertyName = propertyName;
            GlobalTypeName = globalTypeName;
            Guid = guid;
        }

        public string PropertyName { get; }
        public string GlobalTypeName { get; }
        public int Guid { get; }
    }
}
