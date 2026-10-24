using System.Globalization;
using System.Text;

namespace FellowshipAnalyzer.Generators;

internal readonly struct JsonValue
{
    public Dictionary<string, JsonValue>? Object { get; }
    public List<JsonValue>? Array { get; }
    public string? String { get; }
    public double? Number { get; }
    public bool? Bool { get; }

    private JsonValue(Dictionary<string, JsonValue>? o, List<JsonValue>? a, string? s, double? n, bool? b = null)
    {
        Object = o;
        Array = a;
        String = s;
        Number = n;
        Bool = b;
    }

    public static JsonValue Obj(Dictionary<string, JsonValue> o) => new(o, null, null, null);
    public static JsonValue Arr(List<JsonValue> a) => new(null, a, null, null);
    public static JsonValue Str(string s) => new(null, null, s, null);
    public static JsonValue Num(double n) => new(null, null, null, n);
    public static JsonValue Boolean(bool b) => new(null, null, null, null, b);
    public static JsonValue Empty() => new(null, null, null, null);
}

internal static class JsonParser
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
            case 't': pos += 4; return JsonValue.Boolean(true);
            case 'f': pos += 5; return JsonValue.Boolean(false);
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
