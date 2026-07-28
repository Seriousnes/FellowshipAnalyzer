using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Emits every spell registry from the committed <c>data/spelldb.json</c> (scope → member → entry):
/// the central <c>Spells</c> trigger class (<c>shared</c> scope members + aggregate <c>All</c>),
/// the <c>Items</c> registry (<c>items</c> scope), and one <c>Spells</c> registry per hero scope. Each member is
/// typed by its <c>kind</c> (<c>ability</c>→<see cref="object"/> <c>Spell</c>, <c>effect</c>→<c>Effect</c>,
/// <c>talent</c>→<c>Talent</c>, <c>weapon</c>→<c>Weapon</c>).
/// </summary>
/// <remarks>
/// Stays dormant when no <c>spelldb.json</c> <c>AdditionalFile</c> is present; it returns without emitting anything.
/// </remarks>
[Generator]
public sealed class ConsolidatedSpellRegistryGenerator : IIncrementalGenerator
{
    private const string SpellsNamespace = "FellowshipAnalyzer.Core.Common.Spells";
    private const string ItemsNamespace = "FellowshipAnalyzer.Core.Common.Items";
    private const string AttributeName = "GenerateRegistryAttribute";
    private const string SpellDbFileName = "spelldb.json";
    private const string SharedScope = "shared";
    private const string ItemsScope = "items";

    private static readonly DiagnosticDescriptor DuplicateMemberDescriptor = new(
        id: "FA0001",
        title: "Duplicate member name in spell scope",
        messageFormat: "Member '{0}' is defined more than once in scope '{1}'. Rename one to resolve the conflict.",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownScopeDescriptor = new(
        id: "FA0005",
        title: "Unknown spell scope",
        messageFormat: "Scope '{0}' names no known registry; expected 'shared', 'items', or a hero name",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingIdDescriptor = new(
        id: "FA0006",
        title: "Spell member without id",
        messageFormat: "Member '{0}' in scope '{1}' has no 'id' and was skipped",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownKindDescriptor = new(
        id: "FA0007",
        title: "Spell member with unresolvable kind",
        messageFormat: "Member '{0}' in scope '{1}' has kind '{2}' whose Spell subtype is not present in Core and was skipped",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateFSLIDDescriptor = new(
        id: "FA0008",
        title: "Duplicate spell FSLID",
        messageFormat: "FSLID {0} is produced by both '{1}' and '{2}'; keeping the first",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownPropertyKeyDescriptor = new(
        id: "FA0009",
        title: "Spell entry key matches no Spell property",
        messageFormat: "Key '{0}' on member '{1}' in scope '{2}' matches no settable Spell property and was ignored",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownCostKeyDescriptor = new(
        id: "FA0010",
        title: "Cost key matches no ResourceTypes member",
        messageFormat: "Cost key '{0}' on member '{1}' in scope '{2}' matches no ResourceTypes member and was ignored",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ScopeNotHeroNameDescriptor = new(
        id: "FA0011",
        title: "Spell scope is not a known hero",
        messageFormat: "Scope '{0}' is not 'shared', 'items', or a HeroName member",
        category: "Registry",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var centralTriggers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsPartialStaticClassWithAttributes(node),
                transform: static (ctx, ct) => GetCentralTrigger(ctx, ct))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!)
            .Collect();

        var spellDb = context.AdditionalTextsProvider
            .Where(static f => f.Path.Replace('\\', '/').EndsWith(SpellDbFileName, StringComparison.OrdinalIgnoreCase))
            .Select(static (f, ct) => f.GetText(ct)?.ToString())
            .Where(static s => !string.IsNullOrEmpty(s))
            .Select(static (s, _) => s!)
            .Collect();

        var combined = spellDb.Combine(centralTriggers).Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, data) =>
            Execute(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    private static bool IsPartialStaticClassWithAttributes(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl || classDecl.AttributeLists.Count == 0)
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

    private static CentralTriggerInfo? GetCentralTrigger(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        bool isTrigger = false;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == AttributeName &&
                attr.AttributeClass.IsGenericType &&
                attr.AttributeClass.TypeArguments.Length == 1)
            {
                isTrigger = true;
                break;
            }
        }

        if (!isTrigger)
            return null;

        var members = ImmutableArray.CreateBuilder<CentralMember>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol prop || !prop.IsStatic || prop.GetMethod is null)
                continue;
            if (!HasFslidProperty(prop.Type))
                continue;
            if (!TryComputeFslid(prop, ct, out var guid))
                continue;

            members.Add(new CentralMember(prop.Name, GlobalName(prop.Type), guid));
        }

        return new CentralTriggerInfo(symbol.Name, NamespaceOf(symbol), members.ToImmutable());
    }

    private static void Execute(
        SourceProductionContext spc,
        ImmutableArray<string> spellDbTexts,
        ImmutableArray<CentralTriggerInfo> centralTriggers,
        Compilation compilation)
    {
        if (spellDbTexts.IsDefaultOrEmpty)
            return;

        JsonValue root;
        try { root = JsonParser.Parse(spellDbTexts[0]); }
        catch { return; }
        if (root.Object is not { } scopes)
            return;

        var central = centralTriggers.IsDefaultOrEmpty ? null : centralTriggers[0];
        var centralNamespace = central?.Namespace ?? SpellsNamespace;
        var centralClassName = central?.ClassName ?? "Spells";
        var centralGlobalName = "global::" + centralNamespace + "." + centralClassName;

        var spellType = compilation.GetTypeByMetadataName(SpellsNamespace + ".Spell");
        var kindTypes = new Dictionary<string, KindInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["ability"] = new KindInfo("Spell", 0, compilation.GetTypeByMetadataName(SpellsNamespace + ".Spell")),
            ["effect"] = new KindInfo("Effect", 1_000_000, compilation.GetTypeByMetadataName(SpellsNamespace + ".Effect")),
            ["talent"] = new KindInfo("Talent", 2_000_000, compilation.GetTypeByMetadataName(SpellsNamespace + ".Talent")),
            ["weapon"] = new KindInfo("Weapon", 3_000_000, compilation.GetTypeByMetadataName(SpellsNamespace + ".Weapon")),
        };

        var schema = BuildSchema(compilation, spellType);
        var heroNames = BuildHeroNameSet(compilation);

        var sharedMembers = new List<EmitMember>();
        var registries = new List<RegistryModel>();

        var allEntries = new List<AllEntry>();
        var fslidOwners = new Dictionary<int, string>();
        var lcaTypes = new List<ITypeSymbol>();

        if (central is not null)
        {
            foreach (var hand in central.Members)
            {
                RecordEntry(spc, allEntries, fslidOwners, lcaTypes, centralGlobalName, hand.Name, hand.FSLID,
                    ResolveType(compilation, hand.GlobalTypeName), centralGlobalName + "." + hand.Name);
            }
        }

        foreach (var scopeName in SortedKeys(scopes))
        {
            if (scopes[scopeName].Object is not { } scopeObj)
                continue;

            ScopeTarget target;
            if (string.Equals(scopeName, SharedScope, StringComparison.OrdinalIgnoreCase))
            {
                target = new ScopeTarget(centralNamespace, centralClassName, centralGlobalName, IsCentral: true);
            }
            else if (string.Equals(scopeName, ItemsScope, StringComparison.OrdinalIgnoreCase))
            {
                target = new ScopeTarget(ItemsNamespace, "Items", "global::" + ItemsNamespace + ".Items", IsCentral: false);
            }
            else if (IsValidIdentifierSeed(scopeName))
            {
                var hero = Pascal(scopeName);
                if (heroNames is not null && !heroNames.Contains(hero))
                    spc.ReportDiagnostic(Diagnostic.Create(ScopeNotHeroNameDescriptor, Location.None, scopeName));
                var ns = SpellsNamespace + "." + hero;
                target = new ScopeTarget(ns, "Spells", "global::" + ns + ".Spells", IsCentral: false);
            }
            else
            {
                spc.ReportDiagnostic(Diagnostic.Create(UnknownScopeDescriptor, Location.None, scopeName));
                continue;
            }

            var members = new List<EmitMember>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var memberName in SortedKeys(scopeObj))
            {
                if (scopeObj[memberName].Object is not { } entry)
                    continue;

                if (!seen.Add(memberName))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(DuplicateMemberDescriptor, Location.None, memberName, scopeName));
                    continue;
                }

                if (!entry.TryGetValue("id", out var idValue) || idValue.Number is not { } idNum)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MissingIdDescriptor, Location.None, memberName, scopeName));
                    continue;
                }

                var id = (int)Math.Round(idNum);
                var kind = entry.TryGetValue("kind", out var kindValue) && kindValue.String is { } ks ? ks : "ability";

                if (!kindTypes.TryGetValue(kind, out var kindInfo) || kindInfo.Type is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(UnknownKindDescriptor, Location.None, memberName, scopeName, kind));
                    continue;
                }

                var guid = kindInfo.Offset + id;
                var initLines = BuildInitLines(spc, scopeName, memberName, id, entry, schema);
                members.Add(new EmitMember(kindInfo.TypeName, memberName, guid, initLines));

                RecordEntry(spc, allEntries, fslidOwners, lcaTypes, target.GlobalName, memberName, guid,
                    kindInfo.Type, target.GlobalName + "." + memberName);
            }

            if (target.IsCentral)
                sharedMembers.AddRange(members);
            else
                registries.Add(new RegistryModel(target.Namespace, target.ClassName, members));
        }

        var lca = ComputeLcaType(lcaTypes) ?? spellType;
        var lcaGlobalName = lca is not null ? GlobalName(lca) : "global::" + SpellsNamespace + ".Spell";

        foreach (var registry in registries)
            EmitRegistry(spc, registry);

        EmitCentral(spc, centralNamespace, centralClassName, central, sharedMembers, allEntries, lcaGlobalName);
    }

    private static void RecordEntry(
        SourceProductionContext spc,
        List<AllEntry> allEntries,
        Dictionary<int, string> fslidOwners,
        List<ITypeSymbol> lcaTypes,
        string containerGlobalName,
        string memberName,
        int guid,
        ITypeSymbol? type,
        string memberAccess)
    {
        if (fslidOwners.TryGetValue(guid, out var existing))
        {
            spc.ReportDiagnostic(Diagnostic.Create(DuplicateFSLIDDescriptor, Location.None, guid, existing, memberAccess));
            return;
        }

        fslidOwners[guid] = memberAccess;
        allEntries.Add(new AllEntry(containerGlobalName, memberName, guid));
        if (type is not null)
            lcaTypes.Add(type);
    }

    private static List<string> BuildInitLines(
        SourceProductionContext spc, string scopeName, string memberName,
        int id, Dictionary<string, JsonValue> entry, SpellSchema schema)
    {
        var parts = new List<string> { "Id = " + id.ToString(CultureInfo.InvariantCulture) };

        foreach (var prop in schema.Scalars)
        {
            if (!entry.TryGetValue(prop.JsonKey, out var v))
                continue;
            switch (prop.Kind)
            {
                case ScalarKind.String:
                    parts.Add(prop.Name + " = " + Literal(v.String ?? ""));
                    break;
                case ScalarKind.Int when v.Number is { } ni:
                    parts.Add(prop.Name + " = " + ((int)Math.Round(ni)).ToString(CultureInfo.InvariantCulture));
                    break;
                case ScalarKind.Double when v.Number is { } nd:
                    parts.Add(prop.Name + " = " + Fmt(nd));
                    break;
                case ScalarKind.Enum when v.String is { } es && prop.EnumMembers is { } members && members.Contains(es):
                    parts.Add(prop.Name + " = " + prop.EnumGlobalName + "." + es);
                    break;
            }
        }

        foreach (var key in entry.Keys)
        {
            if (!schema.KnownJsonKeys.Contains(key))
                spc.ReportDiagnostic(Diagnostic.Create(UnknownPropertyKeyDescriptor, Location.None, key, memberName, scopeName));
        }

        if (entry.TryGetValue("costs", out var costsValue) && costsValue.Object is { } costs)
        {
            var costParts = new List<string>();
            foreach (var costKey in costs.Keys)
            {
                if (costs[costKey].Number is not { } cv)
                    continue;
                if (schema.CostTokens.TryGetValue(costKey, out var memberAccess))
                    costParts.Add("[" + memberAccess + "] = " + ((int)Math.Round(cv)).ToString(CultureInfo.InvariantCulture));
                else
                    spc.ReportDiagnostic(Diagnostic.Create(UnknownCostKeyDescriptor, Location.None, costKey, memberName, scopeName));
            }
            if (costParts.Count > 0)
                parts.Add("Costs = new global::System.Collections.Generic.Dictionary<" +
                          schema.ResourceTypesGlobalName + ", int> { " + string.Join(", ", costParts) + " }");
        }

        return parts;
    }

    private static SpellSchema BuildSchema(Compilation compilation, ITypeSymbol? spellType)
    {
        var scalars = new List<ScalarProp>();
        var knownKeys = new HashSet<string>(StringComparer.Ordinal) { "id", "kind", "costs" };
        var costTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var resourceTypesGlobal = "global::FellowshipAnalyzer.Core.Game.ResourceTypes";

        if (spellType is not null)
        {
            foreach (var member in spellType.GetMembers())
            {
                if (member is not IPropertySymbol p || p.SetMethod is null)
                    continue;
                if (p.Name is "Id" or "Costs")
                    continue;
                var key = CamelCase(p.Name);
                if (ClassifyScalar(p.Type) is { } kind)
                {
                    scalars.Add(new ScalarProp(p.Name, key, kind));
                    knownKeys.Add(key);
                }
                else if (TryClassifyEnum(p.Type, out var enumGlobal, out var enumMembers))
                {
                    scalars.Add(new ScalarProp(p.Name, key, ScalarKind.Enum, enumGlobal, enumMembers));
                    knownKeys.Add(key);
                }
            }
        }

        var resourceTypes = compilation.GetTypeByMetadataName("FellowshipAnalyzer.Core.Game.ResourceTypes");
        if (resourceTypes is not null)
        {
            resourceTypesGlobal = GlobalName(resourceTypes);
            foreach (var member in resourceTypes.GetMembers())
                if (member is IFieldSymbol { IsConst: true } f)
                    costTokens[CamelCase(f.Name)] = resourceTypesGlobal + "." + f.Name;
        }

        return new SpellSchema(scalars, costTokens, knownKeys, resourceTypesGlobal);
    }

    private static HashSet<string>? BuildHeroNameSet(Compilation compilation)
    {
        var heroName = compilation.GetTypeByMetadataName("FellowshipAnalyzer.Core.Analysis.HeroName");
        if (heroName is null)
            return null;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in heroName.GetMembers())
            if (member is IFieldSymbol { IsConst: true } f)
                set.Add(f.Name);
        return set;
    }

    private static ScalarKind? ClassifyScalar(ITypeSymbol type)
    {
        var t = type;
        if (t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nt)
            t = nt.TypeArguments[0];
        return t.SpecialType switch
        {
            SpecialType.System_Int32 => ScalarKind.Int,
            SpecialType.System_Double => ScalarKind.Double,
            SpecialType.System_String => ScalarKind.String,
            _ => null,
        };
    }

    private static bool TryClassifyEnum(ITypeSymbol type, out string enumGlobalName, out HashSet<string> members)
    {
        enumGlobalName = "";
        members = new HashSet<string>(StringComparer.Ordinal);
        var t = type;
        if (t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nt)
            t = nt.TypeArguments[0];
        if (t.TypeKind != TypeKind.Enum)
            return false;
        enumGlobalName = GlobalName(t);
        foreach (var member in t.GetMembers())
            if (member is IFieldSymbol { IsConst: true } f)
                members.Add(f.Name);
        return true;
    }

    private static string CamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static void EmitRegistry(SourceProductionContext spc, RegistryModel registry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace " + registry.Namespace + ";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Spell definitions generated from <c>data/spelldb.json</c>.</summary>");
        sb.AppendLine("public partial class " + registry.ClassName + " : global::" + SpellsNamespace + ".ISpellRegistry");
        sb.AppendLine("{");
        AppendMembers(sb, registry.Members);
        sb.AppendLine("}");

        spc.AddSource(HintName(registry.Namespace, registry.ClassName), sb.ToString());
    }

    private static void EmitCentral(
        SourceProductionContext spc,
        string ns,
        string className,
        CentralTriggerInfo? central,
        List<EmitMember> sharedMembers,
        List<AllEntry> allEntries,
        string lcaGlobalName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace " + ns + ";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Spell definitions shared by every hero, generated from <c>data/spelldb.json</c>.</summary>");
        sb.AppendLine("public static partial class " + className);
        sb.AppendLine("{");

        if (sharedMembers.Count > 0)
        {
            AppendMembers(sb, sharedMembers);
            sb.AppendLine();
        }

        sb.AppendLine("    /// <summary>All registered entries keyed by their <c>FSLID</c> value.</summary>");
        sb.AppendLine("    public static FrozenDictionary<int, " + lcaGlobalName + "> All { get; } =");
        sb.AppendLine("        new Dictionary<int, " + lcaGlobalName + ">");
        sb.AppendLine("        {");
        foreach (var entry in allEntries)
        {
            sb.AppendLine("            [" + entry.ContainerGlobalName + "." + entry.MemberName + ".FSLID.Value] = " +
                          entry.ContainerGlobalName + "." + entry.MemberName + ",");
        }
        sb.AppendLine("        }.ToFrozenDictionary();");
        sb.AppendLine("}");

        spc.AddSource(HintName(ns, className), sb.ToString());
    }

    private static void AppendMembers(StringBuilder sb, IEnumerable<EmitMember> members)
    {
        foreach (var member in members)
        {
            var typeName = "global::" + SpellsNamespace + "." + member.TypeName;
            sb.AppendLine("    /// <summary>" + DisplayName(member) + " (FSLID " +
                          member.FSLID.ToString(CultureInfo.InvariantCulture) + ").</summary>");
            sb.AppendLine("    [global::" + SpellsNamespace + ".SpellId(" +
                          member.FSLID.ToString(CultureInfo.InvariantCulture) + ")]");
            sb.AppendLine("    public static " + typeName + " " + member.Name + " { get; } = new " +
                          typeName + " { " + string.Join(", ", member.InitLines) + " };");
            sb.AppendLine();
        }
    }

    private static string DisplayName(EmitMember member)
    {
        foreach (var line in member.InitLines)
        {
            if (!line.StartsWith("Name = \"", StringComparison.Ordinal) || !line.EndsWith("\"", StringComparison.Ordinal))
                continue;
            var value = line.Substring("Name = \"".Length, line.Length - "Name = \"".Length - 1);
            if (value.Length == 0 || value.IndexOf('\\') >= 0)
                break;
            return XmlEscape(value);
        }
        return "<c>" + member.Name + "</c>";
    }

    private static string XmlEscape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string HintName(string ns, string className) =>
        (string.IsNullOrEmpty(ns) ? className : ns.Replace('.', '_') + "_" + className) + ".Consolidated.g.cs";

    private static IEnumerable<string> SortedKeys(Dictionary<string, JsonValue> obj)
    {
        var keys = new List<string>(obj.Keys);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static bool IsValidIdentifierSeed(string scope)
    {
        if (scope.Length == 0)
            return false;
        if (!char.IsLetter(scope[0]) && scope[0] != '_')
            return false;
        foreach (var c in scope)
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        return true;
    }

    private static string Pascal(string scope) =>
        char.ToUpperInvariant(scope[0]) + scope.Substring(1);

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    private static string Fmt(double v) =>
        v == Math.Floor(v) && !double.IsInfinity(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("R", CultureInfo.InvariantCulture);

    private static ITypeSymbol? ResolveType(Compilation compilation, string globalTypeName)
    {
        var metadataName = globalTypeName.StartsWith("global::", StringComparison.Ordinal)
            ? globalTypeName.Substring("global::".Length)
            : globalTypeName;
        return compilation.GetTypeByMetadataName(metadataName);
    }

    private static ITypeSymbol? ComputeLcaType(List<ITypeSymbol> types)
    {
        ITypeSymbol? lca = null;
        foreach (var type in types)
            lca = lca is null ? type : ComputeLca(lca, type);
        return lca;
    }

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

    private static bool TryComputeFslid(IPropertySymbol property, CancellationToken ct, out int fslid)
    {
        fslid = 0;
        if (!TryReadCtorInt(property, ct, out var id)) return false;
        fslid = RangeOffsetForType(property.Type) + id;
        return true;
    }

    private static int RangeOffsetForType(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (current.ContainingNamespace?.ToDisplayString() == SpellsNamespace)
            {
                switch (current.Name)
                {
                    case "Weapon": return 3_000_000;
                    case "Talent": return 2_000_000;
                    case "Effect": return 1_000_000;
                }
            }
            current = current.BaseType;
        }
        return 0;
    }

    private static bool TryReadCtorInt(IPropertySymbol property, CancellationToken ct, out int id)
    {
        id = 0;
        if (property.DeclaringSyntaxReferences.Length == 0) return false;
        if (property.DeclaringSyntaxReferences[0].GetSyntax(ct) is not PropertyDeclarationSyntax pds) return false;
        if (pds.Initializer is not { } initializer) return false;

        var initExpr = initializer.Value switch
        {
            ObjectCreationExpressionSyntax o => o.Initializer,
            ImplicitObjectCreationExpressionSyntax io => io.Initializer,
            _ => null,
        };
        if (initExpr is null) return false;
        foreach (var e in initExpr.Expressions)
            if (e is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "Id" } } a
                && a.Right is LiteralExpressionSyntax { Token.Value: int initId })
            { id = initId; return true; }
        return false;
    }

    private static bool HasFslidProperty(ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            foreach (var member in current.GetMembers("FSLID"))
                if (member is IPropertySymbol { GetMethod: not null })
                    return true;
            current = (current as INamedTypeSymbol)?.BaseType;
        }
        return false;
    }

    private static string GlobalName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace?.IsGlobalNamespace == false
            ? type.ContainingNamespace.ToDisplayString()
            : string.Empty;

    private sealed record CentralTriggerInfo(string ClassName, string Namespace, ImmutableArray<CentralMember> Members);

    private readonly record struct CentralMember(string Name, string GlobalTypeName, int FSLID);

    private readonly record struct KindInfo(string TypeName, int Offset, ITypeSymbol? Type);

    private readonly record struct ScopeTarget(string Namespace, string ClassName, string GlobalName, bool IsCentral);

    private sealed record RegistryModel(string Namespace, string ClassName, List<EmitMember> Members);

    private readonly record struct EmitMember(string TypeName, string Name, int FSLID, List<string> InitLines);

    private readonly record struct AllEntry(string ContainerGlobalName, string MemberName, int FSLID);

    private enum ScalarKind { Int, Double, String, Enum }

    private readonly record struct ScalarProp(
        string Name, string JsonKey, ScalarKind Kind,
        string? EnumGlobalName = null, HashSet<string>? EnumMembers = null);

    private sealed record SpellSchema(
        IReadOnlyList<ScalarProp> Scalars,
        IReadOnlyDictionary<string, string> CostTokens,
        HashSet<string> KnownJsonKeys,
        string ResourceTypesGlobalName);

    private readonly struct JsonValue
    {
        public Dictionary<string, JsonValue>? Object { get; }
        public List<JsonValue>? Array { get; }
        public string? String { get; }
        public double? Number { get; }

        private JsonValue(Dictionary<string, JsonValue>? o, List<JsonValue>? a, string? s, double? n)
        {
            Object = o;
            Array = a;
            String = s;
            Number = n;
        }

        public static JsonValue Obj(Dictionary<string, JsonValue> o) => new(o, null, null, null);
        public static JsonValue Arr(List<JsonValue> a) => new(null, a, null, null);
        public static JsonValue Str(string s) => new(null, null, s, null);
        public static JsonValue Num(double n) => new(null, null, null, n);
        public static JsonValue Empty() => new(null, null, null, null);
    }

    private static class JsonParser
    {
        public static JsonValue Parse(string text)
        {
            int pos = 0;
            SkipWhitespace(text, ref pos);
            var value = ParseValue(text, ref pos);
            return value;
        }

        private static JsonValue ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return JsonValue.Str(ParseString(s, ref pos));
                case 't': pos += 4; return JsonValue.Empty();
                case 'f': pos += 5; return JsonValue.Empty();
                case 'n': pos += 4; return JsonValue.Empty();
                default: return JsonValue.Num(ParseNumber(s, ref pos));
            }
        }

        private static JsonValue ParseObject(string s, ref int pos)
        {
            var result = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            pos++;
            SkipWhitespace(s, ref pos);
            if (s[pos] == '}') { pos++; return JsonValue.Obj(result); }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                var key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                pos++;
                var value = ParseValue(s, ref pos);
                result[key] = value;
                SkipWhitespace(s, ref pos);
                char c = s[pos++];
                if (c == ',') continue;
                if (c == '}') break;
            }
            return JsonValue.Obj(result);
        }

        private static JsonValue ParseArray(string s, ref int pos)
        {
            var result = new List<JsonValue>();
            pos++;
            SkipWhitespace(s, ref pos);
            if (s[pos] == ']') { pos++; return JsonValue.Arr(result); }
            while (true)
            {
                result.Add(ParseValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                char c = s[pos++];
                if (c == ',') continue;
                if (c == ']') break;
            }
            return JsonValue.Arr(result);
        }

        private static string ParseString(string s, ref int pos)
        {
            pos++;
            var sb = new StringBuilder();
            while (true)
            {
                char c = s[pos++];
                if (c == '"') break;
                if (c == '\\')
                {
                    char e = s[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            var hex = s.Substring(pos, 4);
                            pos += 4;
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c is '-' or '+' or '.' or 'e' or 'E' || (c >= '0' && c <= '9'))
                    pos++;
                else
                    break;
            }
            return double.Parse(s.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c is ' ' or '\t' or '\n' or '\r')
                    pos++;
                else
                    break;
            }
        }
    }
}
