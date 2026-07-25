using System.Reflection;
using FellowshipAnalyzer.Core.Contracts.Design;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Design;

/// <summary>
/// The committed <c>_palette.scss</c> is what a fresh clone compiles before any tool runs, so a
/// hand edit or a bad merge there would silently override the C# theme. These tests close that hole.
/// </summary>
public class PaletteScssDriftTests
{
    [Fact]
    public void Committed_Palette_Matches_The_Theme()
    {
        var path = ResolvePalettePath();

        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");
        var emitted = FaPaletteScss.Render().ReplaceLineEndings("\n");

        committed.ShouldBe(
            emitted,
            $"{FaPaletteScss.RelativePath} has drifted from FaTheme. " +
            "Run: dotnet run --no-cache emit-palette.cs <path> from src/FellowshipAnalyzer.Tools.");
    }

    [Fact]
    public void Every_Theme_Defines_Every_Token()
    {
        var expected = FaTheme.Original.Tokens.Select(t => t.Name).ToList();

        expected.Count.ShouldBe(96);

        foreach (var theme in FaTheme.All)
        {
            var tokens = theme.Tokens.ToList();
            tokens.Select(t => t.Name).ShouldBe(expected, $"{theme.Name} declares a different token set.");
            tokens.ShouldAllBe(t => !string.IsNullOrWhiteSpace(t.Value), $"{theme.Name} leaves a token empty.");
        }
    }

    /// <summary>
    /// A palette property that <see cref="FaTheme.Tokens"/> forgets compiles, emits and drift-checks
    /// clean, but resolves to nothing wherever CSS names it. Reflection is used here only; the app
    /// itself never reflects over the design system.
    /// </summary>
    [Fact]
    public void Every_Palette_Colour_Reaches_The_Stylesheet()
    {
        var emitted = FaTheme.Original.Tokens.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        var missing = typeof(FaPalette)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(FaColor) || p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .Where(name => !emitted.Contains(FaToken.KebabCase(name)))
            .ToList();

        missing.ShouldBeEmpty("FaPalette declares a colour that FaTheme.Tokens never yields.");
    }

    /// <summary>
    /// Every reference C# hands to markup has to name a token some theme actually defines.
    /// </summary>
    [Fact]
    public void Every_FaVar_Member_Names_A_Defined_Token()
    {
        var defined = FaTheme.Original.Tokens.Select(t => t.Reference).ToHashSet(StringComparer.Ordinal);

        var members = typeof(FaVar)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToList();

        members.ShouldNotBeEmpty();

        foreach (var member in members)
        {
            var reference = (string)member.GetValue(null)!;
            defined.ShouldContain(reference, $"FaVar.{member.Name} references a token no theme defines.");
        }
    }

    [Fact]
    public void Generated_Scss_Carries_No_Important()
    {
        FaPaletteScss.Render().ShouldNotContain("!important");
    }

    private static string ResolvePalettePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FaPaletteScss.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {FaPaletteScss.RelativePath} above {AppContext.BaseDirectory}.");
    }
}
