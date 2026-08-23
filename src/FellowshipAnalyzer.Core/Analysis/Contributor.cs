namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A person who maintains or contributes to hero analysis. Referenced by identity from
/// <see cref="HeroConfig.Maintainers"/> and <see cref="ChangelogEntry.Contributors"/>, and
/// defined once in <see cref="Contributors"/>.
/// </summary>
public sealed record Contributor(
    string Nickname,
    string Github,
    string? Discord = null,
    string? About = null,
    List<HeroName>? Mains = null)
{
    /// <summary>GitHub avatar image URL.</summary>
    public string AvatarUrl => $"https://github.com/{Github}.png?size=200";

    /// <summary>Public GitHub profile URL.</summary>
    public string GithubUrl => $"https://github.com/{Github}";
}
