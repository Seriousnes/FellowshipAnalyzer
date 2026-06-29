using System.Text.RegularExpressions;

namespace FellowshipAnalyzer.SpellData;

/// <summary>Produces valid PascalCase C# member names from ability names and effect roles.</summary>
public static class MemberNaming
{
    private static readonly Regex PossessiveSuffix = new(@"'[a-z]*", RegexOptions.Compiled);
    private static readonly Regex WordSplit = new(@"[^A-Za-z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Converts a display name into a valid PascalCase C# identifier.
    /// Possessives (<c>'s</c>) are stripped; remaining non-alphanumeric characters act as word separators.
    /// Returns <see cref="string.Empty"/> when <paramref name="name"/> is null or empty.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var stripped = PossessiveSuffix.Replace(name, string.Empty);
        return string.Concat(
            WordSplit.Split(stripped)
                     .Where(p => p.Length > 0)
                     .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    /// <summary>
    /// Returns the C# member name for a linked effect: <c>{abilityMember}{Sanitize(role)}</c>.
    /// </summary>
    public static string EffectMember(string abilityMember, string role) =>
        abilityMember + Sanitize(role);
}
