using System.Text;

namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// One resolved design token: the kebab-cased name under the <c>--fa-</c> prefix, and the CSS text
/// a theme gives it.
/// </summary>
/// <param name="Name">The token name without its <c>--fa-</c> prefix, for example <c>bg-card</c>.</param>
/// <param name="Value">The CSS text, for example <c>#3a4452</c>.</param>
public readonly record struct FaToken(string Name, string Value)
{
    /// <summary>The prefix every design token custom property carries.</summary>
    public const string Prefix = "--fa-";

    /// <summary>The full custom property name, for example <c>--fa-bg-card</c>.</summary>
    public string CustomProperty => Prefix + Name;

    /// <summary>The CSS reference to this token, for example <c>var(--fa-bg-card)</c>.</summary>
    public string Reference => $"var({CustomProperty})";

    /// <summary>
    /// The CSS reference to the token a design property defines, so C# names a token instead of
    /// repeating its value: <c>Var(nameof(FaPalette.BgCard))</c> gives <c>var(--fa-bg-card)</c>.
    /// </summary>
    public static string Var(string propertyName) => $"var({Prefix}{KebabCase(propertyName)})";

    /// <summary>
    /// Turns a PascalCase property name into the kebab-case token name it maps to. The group a
    /// property belongs to contributes no segment, so <c>BgCard</c> becomes <c>bg-card</c>.
    /// </summary>
    public static string KebabCase(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        var builder = new StringBuilder(propertyName.Length + 4);

        for (var i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
