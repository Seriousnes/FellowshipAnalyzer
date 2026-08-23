using System.Text;

namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// Renders the theme graph as the SCSS partial the apps compile against. The emitter writes the
/// result to disk and the drift test compares the committed file with it, so both sides of the
/// drift check are the same code.
/// </summary>
public static class FaPaletteScss
{
    /// <summary>Line separator for the generated file. Always LF, on every platform.</summary>
    public const char NewLine = '\n';

    /// <summary>The path the emitter writes, relative to the repository root.</summary>
    public const string RelativePath = "src/FellowshipAnalyzer.Core/Styles/_palette.scss";

    /// <summary>Renders the whole partial: a block per theme, then the semantic class map.</summary>
    public static string Render()
    {
        var builder = new StringBuilder();

        Line(builder, "// Generated file. Do not edit.");
        Line(builder, "//");
        Line(builder, "// Written by src/FellowshipAnalyzer.Tools/emit-palette.cs from");
        Line(builder, "// FellowshipAnalyzer.Core.Contracts.Design.FaTheme, which owns every value below.");
        Line(builder, "// Change a design value there and rebuild FellowshipAnalyzer.Core.");

        for (var i = 0; i < FaTheme.All.Count; i++)
        {
            var theme = FaTheme.All[i];

            Line(builder, string.Empty);
            Line(builder, $"// {theme.Name}");
            Line(builder, $"{Selector(i, theme)} {{");

            foreach (var token in theme.Tokens)
            {
                Line(builder, $"    {token.CustomProperty}: {token.Value};");
            }

            Line(builder, "}");
        }

        Line(builder, string.Empty);
        Line(builder, "// Semantic class name to the token that colours it. A single loop mints");
        Line(builder, "// .#{$class}, .#{$class}-bordered and .#{$class}-filled from this map.");
        Line(builder, "$fa-semantic: (");

        foreach (var semantic in FaSemantic.All)
        {
            Line(builder, $"    {semantic.ClassName}: {semantic.TokenName},");
        }

        Line(builder, ");");

        Line(builder, string.Empty);
        Line(builder, "@each $class, $token in $fa-semantic {");
        Line(builder, "    .#{$class}          { color: var(--fa-#{$token}); }");
        Line(builder, "    .#{$class}-bordered { border-color: var(--fa-#{$token}); }");
        Line(builder, "    .#{$class}-filled   { background: var(--fa-#{$token}); }");
        Line(builder, "}");

        return builder.ToString();
    }

    private static string Selector(int index, FaTheme theme) =>
        index == 0 ? ":root" : $":root[data-theme='{theme.Selector}']";

    private static void Line(StringBuilder builder, string text) => builder.Append(text).Append(NewLine);
}
