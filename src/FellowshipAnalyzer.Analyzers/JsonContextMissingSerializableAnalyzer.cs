using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FellowshipAnalyzer.Analyzers;

/// <summary>
/// Reports FA0001 for every concrete subtype of a [JsonPolymorphic] type that is not
/// registered on a JsonSerializerContext subclass with [JsonSerializable(typeof(T))].
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonContextMissingSerializableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FA0001";

    /// <summary>Diagnostic property key: simple name of the missing type (e.g. "CastEvent").</summary>
    public const string MissingTypeSimpleNameKey = "MissingTypeSimpleName";

    /// <summary>Diagnostic property key: fully-qualified name of the missing type's namespace.</summary>
    public const string MissingTypeNamespaceKey = "MissingTypeNamespace";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Missing [JsonSerializable] for polymorphic Event subtype",
        messageFormat: "Type '{0}' derives from [JsonPolymorphic] type '{1}' but is not registered on '{2}' with [JsonSerializable(typeof({0}))]",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All concrete subtypes of a [JsonPolymorphic] base must be registered with [JsonSerializable] on the JsonSerializerContext subclass for source-generated serialization to work correctly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (!HasAttributeByName(typeSymbol, "JsonSourceGenerationOptionsAttribute"))
            return;

        if (!InheritsFromByName(typeSymbol, "JsonSerializerContext"))
            return;

        var registeredTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "JsonSerializableAttribute")
                continue;

            if (attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is INamedTypeSymbol registeredType)
            {
                registeredTypes.Add(registeredType);
            }
        }

        var reportLocation = FindUserWrittenLocation(typeSymbol);

        foreach (var registeredType in registeredTypes.ToList())
        {
            if (!HasAttributeByName(registeredType, "JsonPolymorphicAttribute"))
                continue;

            foreach (var subtype in GetConcreteSubtypes(context.Compilation, registeredType, context.CancellationToken))
            {
                if (registeredTypes.Contains(subtype))
                    continue;

                var props = ImmutableDictionary.Create<string, string?>()
                    .Add(MissingTypeSimpleNameKey, subtype.Name)
                    .Add(MissingTypeNamespaceKey, subtype.ContainingNamespace?.IsGlobalNamespace == false
                        ? subtype.ContainingNamespace.ToDisplayString()
                        : string.Empty);

                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    reportLocation,
                    props,
                    subtype.Name,
                    registeredType.Name,
                    typeSymbol.Name));
            }
        }
    }

    private static Location FindUserWrittenLocation(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is ClassDeclarationSyntax classDecl &&
                classDecl.AttributeLists.Any(al =>
                    al.Attributes.Any(a =>
                        a.Name.ToString().EndsWith("JsonSourceGenerationOptions", System.StringComparison.Ordinal))))
            {
                return classDecl.GetLocation();
            }
        }

        return typeSymbol.Locations.Length > 0
            ? typeSymbol.Locations[0]
            : Location.None;
    }

    private static bool HasAttributeByName(ISymbol symbol, string attributeName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeName)
                return true;
        }
        return false;
    }

    private static bool InheritsFromByName(INamedTypeSymbol type, string baseTypeName)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.Name == baseTypeName)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetConcreteSubtypes(
        Compilation compilation,
        INamedTypeSymbol baseType,
        CancellationToken ct)
    {
        foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();

            if (type.IsAbstract)
                continue;

            if (InheritsFrom(type, baseType))
                yield return type;
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
}
