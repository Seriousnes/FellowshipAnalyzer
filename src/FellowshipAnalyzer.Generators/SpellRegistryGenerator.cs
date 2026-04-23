using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Text;

namespace FellowshipAnalyzer.Generators;

[Generator]
public sealed class SpellRegistryGenerator : IIncrementalGenerator
{
    private const string GenerateSpellRegistryAttributeName = "GenerateSpellRegistryAttribute";
    private const string ISpellRegistryName = "ISpellRegistry";
    private const string SpellTypeName = "Spell";

    private static readonly DiagnosticDescriptor DuplicateSpellDescriptor = new(
        id: "FA0001",
        title: "Duplicate spell property name in ISpellRegistry",
        messageFormat: "Spell property '{0}' is defined in both '{1}' and '{2}'. Rename one to resolve the conflict.",
        category: "SpellRegistry",
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
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
        if (symbol == null)
            return null;

        bool hasAttribute = false;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == GenerateSpellRegistryAttributeName)
            {
                hasAttribute = true;
                break;
            }
        }

        if (!hasAttribute)
            return null;

        // Scan the entire compilation for ISpellRegistry implementors
        var registries = new List<RegistryInfo>();
        foreach (var type in GetAllNamedTypes(ctx.SemanticModel.Compilation.Assembly.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();

            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                continue;

            if (!ImplementsISpellRegistry(type))
                continue;

            var entries = new List<SpellEntry>();
            foreach (var member in type.GetMembers())
            {
                if (member is not IPropertySymbol prop)
                    continue;
                if (!prop.IsStatic || prop.GetMethod == null)
                    continue;
                if (!IsSpellOrSubtype(prop.Type))
                    continue;

                entries.Add(new SpellEntry(prop.Name, prop.Type.Name));
            }

            if (entries.Count == 0)
                continue;

            var typeNs = type.ContainingNamespace?.IsGlobalNamespace == false
                ? type.ContainingNamespace.ToDisplayString()
                : string.Empty;

            registries.Add(new RegistryInfo(type.Name, typeNs, entries.ToImmutableArray()));
        }

        // Sort for deterministic output
        registries.Sort(static (a, b) => string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal));

        var triggerNs = symbol.ContainingNamespace?.IsGlobalNamespace == false
            ? symbol.ContainingNamespace.ToDisplayString()
            : string.Empty;

        // Scan the trigger class's own hand-written Spell properties for the All dict.
        // These don't get forwarding properties (they already exist on the partial class).
        var ownEntries = new List<SpellEntry>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol prop)
                continue;
            if (!prop.IsStatic || prop.GetMethod == null)
                continue;
            if (!IsSpellOrSubtype(prop.Type))
                continue;

            ownEntries.Add(new SpellEntry(prop.Name, prop.Type.Name));
        }

        return new TriggerInfo(symbol.Name, triggerNs, registries.ToImmutableArray(), ownEntries.ToImmutableArray());
    }

    private static void Execute(SourceProductionContext ctx, TriggerInfo trigger)
    {
        if (trigger.Registries.IsEmpty && trigger.OwnEntries.IsEmpty)
            return;

        // Check for duplicate property names across all registries
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        bool hasError = false;
        foreach (var registry in trigger.Registries)
        {
            foreach (var entry in registry.Entries)
            {
                if (seen.TryGetValue(entry.PropertyName, out var existingType))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        DuplicateSpellDescriptor,
                        Location.None,
                        entry.PropertyName,
                        existingType,
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
        for (int i = 0; i < trigger.Registries.Length; i++)
        {
            var registry = trigger.Registries[i];
            if (registry.GlobalName == triggerGlobalName)
                continue;

            if (!firstBlock) sb.AppendLine();
            firstBlock = false;
            sb.AppendLine("    // From " + registry.TypeName);
            foreach (var entry in registry.Entries)
            {
                sb.AppendLine("    public static " + entry.PropertyTypeName + " " + entry.PropertyName +
                              " => " + registry.GlobalName + "." + entry.PropertyName + ";");
            }
        }

        // All dictionary — includes own hand-written entries and all registry entries
        sb.AppendLine();
        sb.AppendLine("    /// <summary>All registered spells keyed by <see cref=\"Spell.Guid\"/>.</summary>");
        sb.AppendLine("    public static FrozenDictionary<int, Spell> All { get; } =");
        sb.AppendLine("        new Dictionary<int, Spell>");
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
        sb.AppendLine("}");

        ctx.AddSource("Spells.g.cs", sb.ToString());
    }

    private static bool ImplementsISpellRegistry(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == ISpellRegistryName)
                return true;
        }
        return false;
    }

    private static bool IsSpellOrSubtype(ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            if (current.Name == SpellTypeName)
                return true;
            current = current.BaseType;
        }
        return false;
    }

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
        public TriggerInfo(string className, string ns, ImmutableArray<RegistryInfo> registries, ImmutableArray<SpellEntry> ownEntries)
        {
            ClassName = className;
            Namespace = ns;
            Registries = registries;
            OwnEntries = ownEntries;
        }

        public string ClassName { get; }
        public string Namespace { get; }
        public ImmutableArray<RegistryInfo> Registries { get; }
        public ImmutableArray<SpellEntry> OwnEntries { get; }
    }

    private sealed class RegistryInfo
    {
        public RegistryInfo(string typeName, string ns, ImmutableArray<SpellEntry> entries)
        {
            TypeName = typeName;
            Namespace = ns;
            Entries = entries;
            GlobalName = "global::" + (string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName);
        }

        public string TypeName { get; }
        public string Namespace { get; }
        public string GlobalName { get; }
        public ImmutableArray<SpellEntry> Entries { get; }
    }

    private sealed class SpellEntry
    {
        public SpellEntry(string propertyName, string propertyTypeName)
        {
            PropertyName = propertyName;
            PropertyTypeName = propertyTypeName;
        }

        public string PropertyName { get; }
        public string PropertyTypeName { get; }
    }
}
