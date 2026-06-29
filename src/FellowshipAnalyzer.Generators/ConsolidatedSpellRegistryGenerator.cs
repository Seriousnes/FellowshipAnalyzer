using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Emits every spell registry from the committed <c>data/spelldb.json</c> (scope → member → entry):
/// the central <c>Spells</c> trigger class (<c>shared</c> scope members + aggregate <c>All</c>/<c>Guids</c>),
/// the <c>Items</c> registry (<c>items</c> scope), and one <c>Spells</c> registry per hero scope. Each member is
/// typed by its <c>kind</c> (<c>ability</c>→<see cref="object"/> <c>Spell</c>, <c>effect</c>→<c>Effect</c>,
/// <c>talent</c>→<c>Talent</c>, <c>weapon</c>→<c>Weapon</c>).
/// </summary>
/// <remarks>
/// Stays dormant when no <c>spelldb.json</c> <c>AdditionalFile</c> is present: it returns without emitting
/// anything, so it coexists with the live <c>RegistryGenerator</c> on Core until the cutover wires the file in.
/// </remarks>
[Generator]
public sealed class ConsolidatedSpellRegistryGenerator : IIncrementalGenerator
{
    private const string SpellsNamespace = "FellowshipAnalyzer.Core.Common.Spells";
    private const string AttributeName = "GenerateRegistryAttribute";
    private const string EffectTypeName = "Effect";
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

    private static readonly DiagnosticDescriptor DuplicateGuidDescriptor = new(
        id: "FA0008",
        title: "Duplicate spell guid",
        messageFormat: "Guid {0} is produced by both '{1}' and '{2}'; keeping the first",
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
            if (!HasGuidProperty(prop.Type))
                continue;
            if (!TryComputeGuid(prop, ct, out var guid))
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

        var sharedMembers = new List<EmitMember>();
        var registries = new List<RegistryModel>();

        var allEntries = new List<AllEntry>();
        var guidOwners = new Dictionary<int, string>();
        var lcaTypes = new List<ITypeSymbol>();

        if (central is not null)
        {
            foreach (var hand in central.Members)
            {
                RecordEntry(spc, allEntries, guidOwners, lcaTypes, centralGlobalName, hand.Name, hand.Guid,
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
                target = new ScopeTarget(SpellsNamespace, "Items", "global::" + SpellsNamespace + ".Items", IsCentral: false);
            }
            else if (IsValidIdentifierSeed(scopeName))
            {
                var hero = Pascal(scopeName);
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
                var initLines = BuildInitLines(id, entry);
                members.Add(new EmitMember(kindInfo.TypeName, memberName, guid, initLines));

                RecordEntry(spc, allEntries, guidOwners, lcaTypes, target.GlobalName, memberName, guid,
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
        Dictionary<int, string> guidOwners,
        List<ITypeSymbol> lcaTypes,
        string containerGlobalName,
        string memberName,
        int guid,
        ITypeSymbol? type,
        string memberAccess)
    {
        if (guidOwners.TryGetValue(guid, out var existing))
        {
            spc.ReportDiagnostic(Diagnostic.Create(DuplicateGuidDescriptor, Location.None, guid, existing, memberAccess));
            return;
        }

        guidOwners[guid] = memberAccess;
        allEntries.Add(new AllEntry(containerGlobalName, memberName, guid));
        if (type is not null)
            lcaTypes.Add(type);
    }

    private static List<string> BuildInitLines(int id, Dictionary<string, JsonValue> entry)
    {
        var parts = new List<string>
        {
            "Id = " + id.ToString(CultureInfo.InvariantCulture),
            "Name = " + Literal(entry.TryGetValue("name", out var n) ? n.String ?? "" : ""),
            "Icon = " + Literal(entry.TryGetValue("icon", out var ic) ? ic.String ?? "" : ""),
        };

        AddDouble(parts, entry, "cooldown", "Cooldown");
        AddInt(parts, entry, "range", "Range");
        AddInt(parts, entry, "charges", "Charges");
        AddDouble(parts, entry, "castDuration", "CastDuration");
        AddDouble(parts, entry, "channelDuration", "ChannelDuration");
        AddDouble(parts, entry, "channelTickInterval", "ChannelTickInterval");

        if (entry.TryGetValue("costs", out var costsValue) && costsValue.Object is { } costs)
        {
            AddInt(parts, costs, "spirit", "SpiritCost");
            AddInt(parts, costs, "winterOrb", "WinterOrbCost");
            AddInt(parts, costs, "anima", "AnimaCost");
            AddInt(parts, costs, "focus", "FocusCost");
        }

        return parts;
    }

    private static void AddDouble(List<string> parts, Dictionary<string, JsonValue> obj, string key, string prop)
    {
        if (obj.TryGetValue(key, out var v) && v.Number is { } n)
            parts.Add(prop + " = " + Fmt(n));
    }

    private static void AddInt(List<string> parts, Dictionary<string, JsonValue> obj, string key, string prop)
    {
        if (obj.TryGetValue(key, out var v) && v.Number is { } n)
            parts.Add(prop + " = " + ((int)Math.Round(n)).ToString(CultureInfo.InvariantCulture));
    }

    private static void EmitRegistry(SourceProductionContext spc, RegistryModel registry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace " + registry.Namespace + ";");
        sb.AppendLine();
        sb.AppendLine("public partial class " + registry.ClassName + " : global::" + SpellsNamespace + ".ISpellRegistry");
        sb.AppendLine("{");
        AppendMembers(sb, registry.Members);
        sb.AppendLine();
        AppendGuids(sb, registry.Members.Select(static m => new GuidConst(m.Name, m.Guid)));
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
        sb.AppendLine("public static partial class " + className);
        sb.AppendLine("{");

        if (sharedMembers.Count > 0)
        {
            AppendMembers(sb, sharedMembers);
            sb.AppendLine();
        }

        sb.AppendLine("    /// <summary>All registered entries keyed by <c>Guid</c>.</summary>");
        sb.AppendLine("    public static FrozenDictionary<int, " + lcaGlobalName + "> All { get; } =");
        sb.AppendLine("        new Dictionary<int, " + lcaGlobalName + ">");
        sb.AppendLine("        {");
        foreach (var entry in allEntries)
        {
            sb.AppendLine("            [" + entry.ContainerGlobalName + "." + entry.MemberName + ".Guid] = " +
                          entry.ContainerGlobalName + "." + entry.MemberName + ",");
        }
        sb.AppendLine("        }.ToFrozenDictionary();");
        sb.AppendLine();

        var centralGuids = new List<GuidConst>();
        var guidNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in sharedMembers)
            if (guidNames.Add(member.Name))
                centralGuids.Add(new GuidConst(member.Name, member.Guid));
        if (central is not null)
            foreach (var hand in central.Members)
                if (guidNames.Add(hand.Name))
                    centralGuids.Add(new GuidConst(hand.Name, hand.Guid));

        sb.AppendLine("    /// <summary>Compile-time int Guid constants for the central registry's entries.</summary>");
        AppendGuids(sb, centralGuids);
        sb.AppendLine("}");

        spc.AddSource(HintName(ns, className), sb.ToString());
    }

    private static void AppendMembers(StringBuilder sb, IEnumerable<EmitMember> members)
    {
        foreach (var member in members)
        {
            sb.AppendLine("    public static " + member.TypeName + " " + member.Name + " { get; } = new " +
                          member.TypeName + " { " + string.Join(", ", member.InitLines) + " };");
        }
    }

    private static void AppendGuids(StringBuilder sb, IEnumerable<GuidConst> guids)
    {
        sb.AppendLine("    public static class Guids");
        sb.AppendLine("    {");
        foreach (var g in guids)
            sb.AppendLine("        public const int " + g.Name + " = " + g.Guid.ToString(CultureInfo.InvariantCulture) + ";");
        sb.AppendLine("    }");
    }

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

    private static string GlobalName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace?.IsGlobalNamespace == false
            ? type.ContainingNamespace.ToDisplayString()
            : string.Empty;

    private sealed record CentralTriggerInfo(string ClassName, string Namespace, ImmutableArray<CentralMember> Members);

    private readonly record struct CentralMember(string Name, string GlobalTypeName, int Guid);

    private readonly record struct KindInfo(string TypeName, int Offset, ITypeSymbol? Type);

    private readonly record struct ScopeTarget(string Namespace, string ClassName, string GlobalName, bool IsCentral);

    private sealed record RegistryModel(string Namespace, string ClassName, List<EmitMember> Members);

    private readonly record struct EmitMember(string TypeName, string Name, int Guid, List<string> InitLines);

    private readonly record struct AllEntry(string ContainerGlobalName, string MemberName, int Guid);

    private readonly record struct GuidConst(string Name, int Guid);

    // --- Minimal JSON reader (netstandard2.0, dependency-free) ---

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
            pos++; // {
            SkipWhitespace(s, ref pos);
            if (s[pos] == '}') { pos++; return JsonValue.Obj(result); }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                var key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                pos++; // :
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
            pos++; // [
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
            pos++; // opening quote
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
