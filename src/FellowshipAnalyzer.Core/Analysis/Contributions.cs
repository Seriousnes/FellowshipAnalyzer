namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>A group of changelog entries under a heading: the shared framework, or a hero.</summary>
public sealed record ContributionGroup(HeroName? Hero, List<ChangelogEntry> Entries)
{
    /// <summary>True when this group is the shared framework rather than a specific hero.</summary>
    public bool IsCore => Hero is null;
}

/// <summary>
/// Queries hero configs and changelogs for a single contributor: which heroes they maintain and
/// which changes they authored, grouped by Core then by hero.
/// </summary>
public static class Contributions
{
    /// <summary>Heroes whose config lists <paramref name="contributor"/> as a maintainer.</summary>
    public static List<HeroName> MaintainedBy(
        Contributor contributor, IEnumerable<HeroConfigEntry> heroes) =>
        [.. heroes.Where(e => e.Config.Maintainers.Contains(contributor)).Select(e => e.Hero)];

    /// <summary>
    /// Every changelog entry authored by <paramref name="contributor"/>, grouped by Core then by
    /// hero, most-recent first within each group and empty groups omitted.
    /// </summary>
    public static List<ContributionGroup> ForContributor(
        Contributor contributor,
        IEnumerable<HeroConfigEntry> heroes,
        List<ChangelogEntry> coreChangelog)
    {
        var groups = new List<ContributionGroup>();

        var core = coreChangelog
            .Where(c => c.Contributors.Contains(contributor))
            .OrderByDescending(c => c.Date)
            .ToList();
        if (core.Count > 0)
            groups.Add(new ContributionGroup(null, core));

        foreach (var entry in heroes)
        {
            var authored = entry.Config.Changelog
                .Where(c => c.Contributors.Contains(contributor))
                .OrderByDescending(c => c.Date)
                .ToList();
            if (authored.Count > 0)
                groups.Add(new ContributionGroup(entry.Hero, authored));
        }

        return groups;
    }
}
