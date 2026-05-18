using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Text;

namespace FellowshipAnalyzer.Generators;

[Generator]
public sealed class JsonDerivedTypeGenerator : IIncrementalGenerator
{
    private const string JsonPolymorphicAttributeName = "JsonPolymorphicAttribute";
    private const string FabricatedAttributeName = "FabricatedAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var triggerClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsPartialClassWithAttributes(node),
                transform: static (ctx, ct) => GetTriggerInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        context.RegisterSourceOutput(triggerClasses, Execute);
    }

    private static bool IsPartialClassWithAttributes(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
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

        return isPartial && classDecl.AttributeLists.Count > 0;
    }

    private static TriggerInfo? GetTriggerInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        // Must have [JsonPolymorphic] attribute
        bool hasJsonPolymorphic = false;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == JsonPolymorphicAttributeName)
            {
                hasJsonPolymorphic = true;
                break;
            }
        }

        if (!hasJsonPolymorphic)
            return null;

        // Collect all non-abstract derived types from the current compilation assembly
        var derivedTypes = new List<DerivedTypeInfo>();

        foreach (var typeSymbol in GetAllNamedTypes(ctx.SemanticModel.Compilation.Assembly.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();

            if (typeSymbol.IsAbstract)
                continue;

            if (!InheritsFrom(typeSymbol, symbol))
                continue;

            // Walk the full base-type chain for [Fabricated] because Roslyn's
            // GetAttributes() does NOT honour Inherited = true on attributes.
            if (HasFabricatedInChain(typeSymbol))
                continue;

            var name = typeSymbol.Name;
            if (!name.EndsWith("Event"))
                continue;

            var discriminator = name.Substring(0, name.Length - "Event".Length).ToLower();

            var ns = typeSymbol.ContainingNamespace?.IsGlobalNamespace == false
                ? typeSymbol.ContainingNamespace.ToDisplayString()
                : string.Empty;

            derivedTypes.Add(new DerivedTypeInfo(name, ns, discriminator));
        }

        // Alphabetical sort for deterministic output
        derivedTypes.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        var triggerNs = symbol.ContainingNamespace?.IsGlobalNamespace == false
            ? symbol.ContainingNamespace.ToDisplayString()
            : string.Empty;

        return new TriggerInfo(symbol.Name, triggerNs, derivedTypes.ToImmutableArray());
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

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType.OriginalDefinition))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    private static bool HasFabricatedInChain(INamedTypeSymbol type)
    {
        ITypeSymbol? current = type;
        while (current != null)
        {
            foreach (var attr in current.GetAttributes())
            {
                if (attr.AttributeClass?.Name == FabricatedAttributeName)
                    return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void Execute(SourceProductionContext ctx, TriggerInfo info)
    {
        if (info.DerivedTypes.IsEmpty)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("namespace " + info.Namespace + ";");
        sb.AppendLine();

        foreach (var dt in info.DerivedTypes)
        {
            var fullTypeName = string.IsNullOrEmpty(dt.Namespace)
                ? dt.Name
                : dt.Namespace + "." + dt.Name;

            sb.AppendLine("[JsonDerivedType(typeof(" + fullTypeName + "), \"" + dt.Discriminator + "\")]");
        }

        sb.AppendLine("public abstract partial class " + info.ClassName);
        sb.AppendLine("{");
        sb.AppendLine("}");

        ctx.AddSource("Event.g.cs", sb.ToString());
    }

    private sealed class TriggerInfo
    {
        public TriggerInfo(string className, string ns, ImmutableArray<DerivedTypeInfo> derivedTypes)
        {
            ClassName = className;
            Namespace = ns;
            DerivedTypes = derivedTypes;
        }

        public string ClassName { get; }
        public string Namespace { get; }
        public ImmutableArray<DerivedTypeInfo> DerivedTypes { get; }
    }

    private sealed class DerivedTypeInfo
    {
        public DerivedTypeInfo(string name, string ns, string discriminator)
        {
            Name = name;
            Namespace = ns;
            Discriminator = discriminator;
        }

        public string Name { get; }
        public string Namespace { get; }
        public string Discriminator { get; }
    }
}
