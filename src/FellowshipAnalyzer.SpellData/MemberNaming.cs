using System.Text.RegularExpressions;

namespace FellowshipAnalyzer.SpellData;

/// <summary>Produces valid PascalCase C# member names from ability names and effect roles.</summary>
public static partial class MemberNaming
{
    [GeneratedRegex(@"'[a-z]*", RegexOptions.Compiled)]
    private static partial Regex PossessiveSuffixRegex();
    private static readonly Regex PossessiveSuffix = PossessiveSuffixRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex WordSplitRegex();    
    private static readonly Regex WordSplit = WordSplitRegex();

    /// <summary>
    /// Converts a display name into a valid PascalCase C# identifier.
    /// Possessives (<c>'s</c>) are stripped; remaining non-alphanumeric characters act as word separators.
    /// Returns <see cref="string.Empty"/> when <paramref name="name"/> is null or empty.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        return Pascal(PossessiveSuffix.Replace(name, string.Empty));
    }

    /// <summary>
    /// Converts a talent name into a valid PascalCase C# identifier. The apostrophe is dropped and the
    /// letters after it kept (<c>Assassin's Guile</c> becomes <c>AssassinsGuile</c>), <c>&amp;</c> reads as
    /// <c>and</c> (<c>Sword &amp; Board</c> becomes <c>SwordAndBoard</c>), and every other non-alphanumeric
    /// character separates words.
    /// Returns <see cref="string.Empty"/> when <paramref name="name"/> is null or empty.
    /// </summary>
    public static string TalentMember(string? name) =>
        string.IsNullOrEmpty(name)
            ? string.Empty
            : Pascal(name.Replace("'", string.Empty).Replace("&", " and "));

    private static string Pascal(string text)
    {
        return string.Concat(
            WordSplit.Split(text)
                     .Where(p => p.Length > 0)
                     .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    /// <summary>
    /// Returns the C# member name for a linked effect: <c>{abilityMember}{Sanitize(role)}</c>.
    /// </summary>
    public static string EffectMember(string abilityMember, string role) =>
        abilityMember + Sanitize(role);

    /// <summary>
    /// Returns the generated constant name for a tuning row key: each <c>.</c> becomes <c>_</c>, every other
    /// character outside <c>[A-Za-z0-9_]</c> is removed, and a name starting with a digit gets the prefix <c>_</c>.
    /// </summary>
    public static string TuningIdentifier(string key)
    {
        var identifier = string.Concat(key
            .Replace('.', '_')
            .Where(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'));

        return identifier.Length > 0 && char.IsAsciiDigit(identifier[0]) ? "_" + identifier : identifier;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="member"/> is non-empty and every
    /// character satisfies C# identifier rules (first char: letter or underscore; remaining:
    /// letter, digit, or underscore).
    /// </summary>
    public static bool IsValidIdentifier(string member) =>
        member.Length > 0 &&
        (char.IsLetter(member[0]) || member[0] == '_') &&
        member.All(c => char.IsLetterOrDigit(c) || c == '_');    
}
