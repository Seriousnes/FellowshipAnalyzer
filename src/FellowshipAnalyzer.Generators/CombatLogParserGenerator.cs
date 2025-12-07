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

    private static ParserInfo GetParserInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
        if (symbol == null)
            return null;

        // Only generate for concrete parsers that inherit from CombatLogParser
        if (symbol.IsAbstract)
            return null;

        if (!InheritsFromCombatLogParser(symbol))
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
                    ownModules.Add(new TypeInfo(typeArg.Name, ns));
                else if (containingType.Name == AddNormalizerAttributeShortName)
                    normalizerTypes.Add(new TypeInfo(typeArg.Name, ns));
            }
        }

        if (ownModules.Count == 0 && normalizerTypes.Count == 0)
            return null;

        // Extract [HeroAnalyzer("id")] attribute
        string heroId = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == HeroAnalyzerAttributeShortName
                && attr.ConstructorArguments.Length == 1
                && attr.ConstructorArguments[0].Value is string id)
            {
                heroId = id;
                break;
            }
        }

        // Walk base type chain to collect inherited base modules
        var baseModules = new List<TypeInfo>();
        var baseType = symbol.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            CollectModulesFromSymbol(baseType, baseModules);
            if (baseType.Name == CombatLogParserClassName) break;
            baseType = baseType.BaseType;
        }

        var parserNs = GetNamespace(symbol);

        return new ParserInfo(
            symbol.Name,
            parserNs,
            [.. ownModules],
            [.. baseModules],
            [.. normalizerTypes],
            heroId);
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

            modules.Add(new TypeInfo(typeArg.Name, GetNamespace(typeArg)));
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

    private static string StripSuffix(string name, string suffix)
    {
        if (name.Length > suffix.Length && name.EndsWith(suffix))
            return name.Substring(0, name.Length - suffix.Length);
        return name;
    }

    private static void Execute(SourceProductionContext ctx, ParserInfo info)
    {
        EmitConcreteParser(ctx, info);
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

        // Computed properties for OWN modules only (base module properties are on the base class)
        foreach (var m in info.OwnModules)
        {
            var propName = StripSuffix(m.Name, "Analyzer");
            sb.AppendLine("    public " + m.FullyQualifiedName + "? " + propName + " => GetModule<" + m.FullyQualifiedName + ">();");
        }

        sb.AppendLine();

        // GetModuleTypes — base modules first (higher priority), then own modules
        sb.AppendLine("    protected override Type[] GetModuleTypes() =>");
        sb.AppendLine("    [");
        foreach (var m in info.BaseModules)
            sb.AppendLine("        typeof(" + m.FullyQualifiedName + "),");
        foreach (var m in info.OwnModules)
            sb.AppendLine("        typeof(" + m.FullyQualifiedName + "),");
        sb.AppendLine("    ];");

        // GetNormalizerTypes — only emitted if normalizers are declared
        if (info.NormalizerTypes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    protected override Type[] GetNormalizerTypes() =>");
            sb.AppendLine("    [");
            foreach (var n in info.NormalizerTypes)
        if (info.HeroId != null)
        {
            sb.AppendLine("        services.AddKeyedScoped<IHeroAnalyzer>(\"" + info.HeroId + "\", (sp, _) => sp.GetRequiredService<" + info.ClassName + ">());");
        }
            sb.AppendLine("    ];");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // DI extension method
        var parserBaseName = StripSuffix(info.ClassName, "CombatLogParser");
        sb.AppendLine("public static class " + parserBaseName + "ServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection Add" + parserBaseName + "Analysis(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddScoped<EventEmitter>();");
        sb.AppendLine("        services.AddScoped<" + info.ClassName + ">();");
        if (info.HeroId != null)
        {
            sb.AppendLine("        services.AddKeyedScoped<IHeroAnalyzer>(\"" + info.HeroId + "\", (sp, _) => sp.GetRequiredService<" + info.ClassName + ">());");
        }
        foreach (var m in info.BaseModules)
            sb.AppendLine("        services.AddScoped<" + m.FullyQualifiedName + ">();");
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
        public TypeInfo(string name, string ns)
        {
            Name = name;
            Namespace = ns;
        }
        public string Name { get; }
        public string Namespace { get; }
        public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name;
    }

    private sealed class ParserInfo
    {
        public ParserInfo(
            string cn, string ns,
            ImmutableArray<TypeInfo> ownModules,
            ImmutableArray<TypeInfo> baseModules,
            ImmutableArray<TypeInfo> normalizerTypes,
            string heroId)
        {
            ClassName = cn;
            Namespace = ns;
            OwnModules = ownModules;
            BaseModules = baseModules;
            NormalizerTypes = normalizerTypes;
            HeroId = heroId;
        }
        public string ClassName { get; }
        public string Namespace { get; }
        /// <summary>Modules declared directly on this class.</summary>
        public ImmutableArray<TypeInfo> OwnModules { get; }
        /// <summary>Modules inherited from the base class chain (collected via GetAttributes on base symbols).</summary>
        public ImmutableArray<TypeInfo> BaseModules { get; }
        public ImmutableArray<TypeInfo> NormalizerTypes { get; }
        /// <summary>Hero ID from [HeroAnalyzer] attribute, if present.</summary>
        public string HeroId { get; }
    }
}

