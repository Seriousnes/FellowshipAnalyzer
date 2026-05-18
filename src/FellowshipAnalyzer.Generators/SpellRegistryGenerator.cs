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

        // Find [GenerateRegistry<T>] and extract the registry interface symbol T.
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

        // Scan the compilation for all concrete types implementing the registry interface.
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

                entries.Add(new EntryProperty(prop.Name, GlobalName(prop.Type)));
                lcaType = lcaType == null ? prop.Type : ComputeLca(lcaType, prop.Type);
            }

            if (entries.Count == 0)
                continue;

            registries.Add(new RegistryInfo(type.Name, NamespaceOf(type), entries.ToImmutableArray()));
        }

        // Sort for deterministic output.
        registries.Sort(static (a, b) => string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal));

        // Scan the trigger class's own hand-written entry properties.
        // These don't get forwarding properties (they're already on the partial class), but are
        // included in the All dictionary.
        var ownEntries = new List<EntryProperty>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;
            if (!prop.IsStatic || prop.GetMethod == null)
                continue;
            if (!HasGuidProperty(prop.Type))
                continue;

            ownEntries.Add(new EntryProperty(prop.Name, GlobalName(prop.Type)));
            lcaType = lcaType == null ? prop.Type : ComputeLca(lcaType, prop.Type);
        }

        var lcaGlobalName = lcaType != null ? GlobalName(lcaType) : null;

        return new TriggerInfo(
            symbol.Name,
            NamespaceOf(symbol),
            registries.ToImmutableArray(),
            ownEntries.ToImmutableArray(),
            lcaGlobalName);
    }

    private static void Execute(SourceProductionContext ctx, TriggerInfo trigger)
    {
        if (trigger.Registries.IsEmpty && trigger.OwnEntries.IsEmpty)
            return;

        // Check for duplicate property names across all registries.
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

        // Forwarding properties, grouped by source registry.
        // The trigger class itself is skipped — its properties are hand-written on the partial class.
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

        // All dictionary — value type is the LCA of all entry types.
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

        sb.AppendLine("}");

        ctx.AddSource(trigger.ClassName + ".g.cs", sb.ToString());
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

        return a; // fallback — should not be reached for well-typed registries
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
        public EntryProperty(string propertyName, string globalTypeName)
        {
            PropertyName = propertyName;
            GlobalTypeName = globalTypeName;
        }

        public string PropertyName { get; }
        public string GlobalTypeName { get; }
    }
}
