using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace FellowshipAnalyzer.Generators;

/// <summary>
/// Emits an <c>AbilityFacts</c> class per hero from the fs_tc_uploads <c>s3/hero_data.json</c>
/// game-data export (referenced via the <c>external/fs_tc_uploads</c> submodule). Each class lives in that hero's own
/// <c>FellowshipAnalyzer.Core.Common.Spells.{Hero}</c> namespace and exposes one
/// <c>SpellbookAbility</c> per kit ability, pre-filled with the data-derived scalars
/// (cooldown, range, charges, cast/channel/tick). Behaviour fields (category, GCD,
/// haste flag, talent branches) are composed by the hand-authored spellbook via <c>with</c>.
/// </summary>
[Generator]
public sealed class AbilityFactsGenerator : IIncrementalGenerator
{
    private const string SpellsRoot = "FellowshipAnalyzer.Core.Common.Spells";
    private const string EffectTypeName = "Effect";
    private const string HeroDataRelativePath = "s3/hero_data.json";
    private const string SpellbookAbilityType = "global::FellowshipAnalyzer.Core.Analysis.SpellbookAbility";
    private const string UncategorizedExpr = "global::FellowshipAnalyzer.Core.Analysis.SpellCategory.Uncategorized";

    private static readonly DiagnosticDescriptor ConflictDescriptor = new(
        id: "FA0100",
        title: "Conflicting ability-facts field",
        messageFormat: "Hero '{0}' ability '{1}' field '{2}' has conflicting values ({3}) across merged Constants entries; using the first.",
        category: "AbilityFacts",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registries = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.ValueText == "Spells",
                transform: static (ctx, ct) => GetHeroRegistry(ctx, ct))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!)
            .Collect();

        var heroData = context.AdditionalTextsProvider
            .Where(static f => f.Path.Replace('\\', '/').EndsWith(HeroDataRelativePath, System.StringComparison.OrdinalIgnoreCase))
            .Select(static (f, ct) => f.GetText(ct)?.ToString())
            .Where(static s => !string.IsNullOrEmpty(s))
            .Select(static (s, _) => s!)
            .Collect();

        context.RegisterSourceOutput(registries.Combine(heroData), static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static HeroRegistry? GetHeroRegistry(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol symbol)
            return null;

        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
        var prefix = SpellsRoot + ".";
        if (!ns.StartsWith(prefix, System.StringComparison.Ordinal))
            return null;

        var hero = ns.Substring(prefix.Length);
        if (hero.Length == 0 || hero.Contains("."))
            return null;

        if (!symbol.AllInterfaces.Any(static i => i.Name == "ISpellRegistry"))
            return null;

        var entries = ImmutableArray.CreateBuilder<GuidProperty>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol prop || !prop.IsStatic || prop.GetMethod is null)
                continue;
            if (!TryComputeGuid(prop, ct, out var guid))
                continue;
            entries.Add(new GuidProperty(guid, prop.Name));
        }

        return entries.Count == 0 ? null : new HeroRegistry(hero, ns, entries.ToImmutable());
    }

    private static bool TryComputeGuid(IPropertySymbol property, CancellationToken ct, out int guid)
    {
        guid = 0;
        if (!TryReadCtorInt(property, ct, out var id))
            return false;
        guid = InheritsFromEffect(property.Type) ? 1_000_000 + id : id;
        return true;
    }

    private static bool TryReadCtorInt(IPropertySymbol property, CancellationToken ct, out int id)
    {
        id = 0;
        if (property.DeclaringSyntaxReferences.Length == 0)
            return false;
        if (property.DeclaringSyntaxReferences[0].GetSyntax(ct) is not PropertyDeclarationSyntax pds)
            return false;
        if (pds.Initializer is not { } initializer)
            return false;

        ArgumentListSyntax? argList = initializer.Value switch
        {
            ObjectCreationExpressionSyntax oc => oc.ArgumentList,
            ImplicitObjectCreationExpressionSyntax ioc => ioc.ArgumentList,
            _ => null,
        };
        if (argList is null || argList.Arguments.Count == 0)
            return false;

        var firstExpr = argList.Arguments[0].Expression;
        if (firstExpr is LiteralExpressionSyntax lit && lit.Token.Value is int li)
        {
            id = li;
            return true;
        }
        return false;
    }

    private static bool InheritsFromEffect(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (current.Name == EffectTypeName && current.ContainingNamespace?.ToDisplayString() == SpellsRoot)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static void Execute(SourceProductionContext spc, ImmutableArray<HeroRegistry> registries, ImmutableArray<string> heroDataTexts)
    {
        if (registries.IsDefaultOrEmpty || heroDataTexts.IsDefaultOrEmpty)
            return;

        JsonValue root;
        try { root = JsonParser.Parse(heroDataTexts[0]); }
        catch { return; }
        if (root.Object is not { } heroes)
            return;

        var byHero = new Dictionary<string, Dictionary<int, string>>(System.StringComparer.Ordinal);
        var nsByHero = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var reg in registries)
        {
            if (!byHero.TryGetValue(reg.Hero, out var map))
            {
                map = new Dictionary<int, string>();
                byHero[reg.Hero] = map;
                nsByHero[reg.Hero] = reg.Namespace;
            }
            foreach (var gp in reg.Entries)
                map[gp.Guid] = gp.Property;
        }

        foreach (var kv in byHero)
        {
            if (!heroes.TryGetValue(kv.Key, out var heroNode) || heroNode.Object is not { } heroObj)
                continue;
            EmitHero(spc, kv.Key, nsByHero[kv.Key], kv.Value, heroObj);
        }
    }

    private static void EmitHero(
        SourceProductionContext spc,
        string hero,
        string ns,
        Dictionary<int, string> guidToProperty,
        Dictionary<string, JsonValue> heroObj)
    {
        if (!heroObj.TryGetValue("Kit", out var kitNode) || kitNode.Object is not { } kit)
            return;

        var byDevName = new Dictionary<string, List<Dictionary<string, JsonValue>>>(System.StringComparer.Ordinal);
        if (heroObj.TryGetValue("Constants", out var cNode) && cNode.Object is { } constants)
        {
            foreach (var entry in constants.Values)
            {
                if (entry.Object is not { } co)
                    continue;
                if (co.TryGetValue("DevName", out var dn) && dn.String is { } dev)
                {
                    if (!byDevName.TryGetValue(dev, out var list))
                    {
                        list = new List<Dictionary<string, JsonValue>>();
                        byDevName[dev] = list;
                    }
                    list.Add(co);
                }
            }
        }

        var empty = new List<Dictionary<string, JsonValue>>();
        var facts = new List<KeyValuePair<string, FactScalars>>();
        foreach (var kitEntry in kit)
        {
            if (kitEntry.Value.Object is not { } ko)
                continue;

            int fslid = ko.TryGetValue("FSLID", out var idv) && idv.Number is { } n
                ? (int)n
                : (int.TryParse(kitEntry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) ? k : -1);
            if (fslid < 0 || !guidToProperty.TryGetValue(fslid, out var prop))
                continue;
            if (!ko.TryGetValue("DevName", out var dnv) || dnv.String is not { } devName)
                continue;

            var entries = byDevName.TryGetValue(devName, out var list) ? list : empty;
            facts.Add(new KeyValuePair<string, FactScalars>(prop, Normalize(spc, hero, prop, entries)));
        }

        if (facts.Count == 0)
            return;

        facts.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace " + ns + ";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Data-derived ability scalars generated from the fs_tc_uploads s3/hero_data.json export.</summary>");
        sb.AppendLine("public static class AbilityFacts");
        sb.AppendLine("{");
        bool first = true;
        foreach (var fact in facts)
        {
            if (!first) sb.AppendLine();
            first = false;
            var s = fact.Value;
            sb.AppendLine("    public static " + SpellbookAbilityType + " " + fact.Key + " { get; } = new()");
            sb.AppendLine("    {");
            sb.AppendLine("        PrimarySpell = global::" + ns + ".Spells." + fact.Key + ",");
            sb.AppendLine("        Category = " + UncategorizedExpr + ",");
            if (s.Cooldown is { } cd) sb.AppendLine("        Cooldown = " + Fmt(cd) + ",");
            if (s.Range is { } r) sb.AppendLine("        Range = " + r.ToString(CultureInfo.InvariantCulture) + ",");
            if (s.Charges is { } ch && ch > 1) sb.AppendLine("        Charges = " + ch.ToString(CultureInfo.InvariantCulture) + ",");
            if (s.CastDuration is { } cast) sb.AppendLine("        CastDuration = " + Fmt(cast) + ",");
            if (s.ChannelDuration is { } chan) sb.AppendLine("        ChannelDuration = " + Fmt(chan) + ",");
            if (s.ChannelTickInterval is { } tick) sb.AppendLine("        ChannelTickInterval = " + Fmt(tick) + ",");
            sb.AppendLine("    };");
        }
        sb.AppendLine("}");

        spc.AddSource("AbilityFacts." + hero + ".g.cs", sb.ToString());
    }

    private static FactScalars Normalize(SourceProductionContext spc, string hero, string prop, List<Dictionary<string, JsonValue>> entries)
    {
        double? cooldown = FindNumber(spc, hero, prop, entries, "Cooldown") ?? FindNumber(spc, hero, prop, entries, "RechargeTime");
        double? maxRange = FindNumber(spc, hero, prop, entries, "MaxRange");
        int? range = maxRange is { } mr ? (int)System.Math.Round(mr / 100.0) : null;
        double? chargesValue = FindNumber(spc, hero, prop, entries, "MaxCharges") ?? FindNumber(spc, hero, prop, entries, "NumCharges");
        int? charges = chargesValue is { } cv ? (int)System.Math.Round(cv) : null;
        double? cast = FindNumber(spc, hero, prop, entries, "CastingDuration") ?? FindNumber(spc, hero, prop, entries, "CastTime");
        double? channel = FindNumber(spc, hero, prop, entries, "ChannelingDuration");
        double? tick = FindNumber(spc, hero, prop, entries, "ChannelingTickInterval");
        return new FactScalars(cooldown, range, charges, cast, channel, tick);
    }

    private static double? FindNumber(SourceProductionContext spc, string hero, string prop, List<Dictionary<string, JsonValue>> entries, string key)
    {
        double? found = null;
        bool conflict = false;
        List<double>? values = null;
        foreach (var entry in entries)
        {
            if (!entry.TryGetValue(key, out var v) || v.Number is not { } n)
                continue;
            if (found is null)
                found = n;
            else if (System.Math.Abs(found.Value - n) > 1e-9)
                conflict = true;
            (values ??= new List<double>()).Add(n);
        }

        if (conflict && values is not null)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                ConflictDescriptor, Location.None, hero, prop, key,
                string.Join(", ", values.Select(static x => x.ToString(CultureInfo.InvariantCulture)))));
        }
        return found;
    }

    private static string Fmt(double v) =>
        v == System.Math.Floor(v) && !double.IsInfinity(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("R", CultureInfo.InvariantCulture);

    private sealed class HeroRegistry(string hero, string ns, ImmutableArray<GuidProperty> entries)
    {
        public string Hero { get; } = hero;
        public string Namespace { get; } = ns;
        public ImmutableArray<GuidProperty> Entries { get; } = entries;
    }

    private readonly record struct GuidProperty(int Guid, string Property);

    private readonly record struct FactScalars(
        double? Cooldown,
        int? Range,
        int? Charges,
        double? CastDuration,
        double? ChannelDuration,
        double? ChannelTickInterval);

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
            var result = new Dictionary<string, JsonValue>(System.StringComparer.Ordinal);
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
