namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Registry of people who maintain or contribute to hero analysis. Each contributor is defined
/// once here and referenced by identity from hero configs and changelogs.
/// </summary>
public static class Contributors
{
    public static readonly Contributor Seriousnes = new(
        Nickname: "Seriousnes",
        Github: "Seriousnes",
        Discord: "seriousnes",
        About: "Creator and maintainer of FellowshipAnalyzer.");

    /// <summary>All defined contributors.</summary>
    public static IReadOnlyList<Contributor> All { get; } = [Seriousnes];

    private static readonly IReadOnlyDictionary<string, Contributor> ByNicknameIndex =
        All.ToDictionary(c => c.Nickname, StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves a contributor by nickname, or <c>null</c> when unknown.</summary>
    public static Contributor? ByNickname(string nickname) =>
        ByNicknameIndex.TryGetValue(nickname, out var c) ? c : null;
}
