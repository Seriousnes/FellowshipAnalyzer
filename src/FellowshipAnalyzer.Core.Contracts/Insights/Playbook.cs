using System.Globalization;
using System.Text.RegularExpressions;

namespace FellowshipAnalyzer.Core.Insights;

/// <summary>
/// One hero's insights playbook: <c>data/playbooks/overview.md</c>, which every hero shares, then the hero's
/// <c>overview.md</c> and one <c>{guide}.md</c> per guide, embedded from <c>data/playbooks/{hero}</c>.
/// </summary>
public sealed partial class Playbook
{
    private const string ResourcePrefix = "playbooks/";
    private const string OverviewFile = "overview";
    private const string SharedOverview = ResourcePrefix + OverviewFile + ".md";

    private Playbook(string hero, string overview, IReadOnlyDictionary<string, string> guides)
    {
        Hero = hero;
        Overview = overview;
        Guides = guides;
    }

    /// <summary>Hero name, lower case, as the playbook folder names it.</summary>
    public string Hero { get; }

    /// <summary>The shared overview and the hero's: the message conventions, the Output rules and the hero's example.</summary>
    public string Overview { get; }

    /// <summary>Text of each guide file, keyed by guide name.</summary>
    public IReadOnlyDictionary<string, string> Guides { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9_.])-?\d+(?:\.\d+)?")]
    private static partial Regex NumberPattern();

    /// <summary>The embedded playbook for <paramref name="hero"/>, or null when it has none.</summary>
    public static Playbook? Load(string hero)
    {
        var folder = $"{ResourcePrefix}{hero.ToLowerInvariant()}/";
        var files = ResourceNames()
            .Where(name => name.StartsWith(folder, StringComparison.Ordinal))
            .ToDictionary(name => name[folder.Length..^".md".Length], Read, StringComparer.Ordinal);
        if (!files.Remove(OverviewFile, out var overview))
            return null;
        return new Playbook(hero.ToLowerInvariant(), Read(SharedOverview) + "\n" + overview, files);
    }

    /// <summary>System prompt for one guide's call: the overview and that guide's file.</summary>
    public string For(string guide) => Overview + "\n" + Guides[guide];

    /// <summary>Numbers the overview and <paramref name="guide"/>'s file state.</summary>
    public IEnumerable<double> GuideNumbers(string guide) => Numbers(For(guide));

    /// <summary>Numbers written in digits in <paramref name="text"/>, thousands separators ignored.</summary>
    public static IEnumerable<double> Numbers(string text) =>
        NumberPattern().Matches(text.Replace(",", "", StringComparison.Ordinal))
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture));

    private static IEnumerable<string> ResourceNames() =>
        typeof(Playbook).Assembly.GetManifestResourceNames()
            .Select(name => name.Replace('\\', '/'))
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal));

    private static string Read(string name)
    {
        var assembly = typeof(Playbook).Assembly;
        var actual = assembly.GetManifestResourceNames().First(candidate => candidate.Replace('\\', '/') == name);
        using var stream = assembly.GetManifestResourceStream(actual)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }
}
