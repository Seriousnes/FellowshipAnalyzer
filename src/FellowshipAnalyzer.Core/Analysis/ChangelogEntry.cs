using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A single dated change to a hero's analysis or the shared framework, credited to one or more
/// <see cref="Contributor"/>s. <see cref="Changes"/> is rendered content, authored as Razor markup.
/// </summary>
public sealed record ChangelogEntry(
    DateOnly Date,
    RenderFragment Changes,
    List<Contributor> Contributors);

/// <summary>
/// A single changelog entry tagged with the hero it belongs to, or the shared framework when
/// <see cref="Hero"/> is null. Produced by <see cref="Changelog.All"/> for the home News feed.
/// </summary>
public sealed record ChangelogFeedItem(HeroName? Hero, ChangelogEntry Entry)
{
    /// <summary>True when this entry is a shared-framework change rather than a specific hero's.</summary>
    public bool IsCore => Hero is null;
}

/// <summary>
/// Factory helpers for building changelog lists. Intended for <c>using static</c> in per-hero
/// changelog declarations.
/// </summary>
public static class Changelog
{
    /// <summary>Builds a <see cref="ChangelogEntry"/> credited to one or more contributors.</summary>
    public static ChangelogEntry Change(DateOnly date, RenderFragment changes, params Contributor[] by) =>
        new(date, changes, [.. by]);

    /// <summary>
    /// Every hero changelog entry plus every Core entry as <see cref="ChangelogFeedItem"/>s,
    /// most-recent first, for the home News feed.
    /// </summary>
    public static List<ChangelogFeedItem> All(
        IEnumerable<HeroConfigEntry> heroes,
        List<ChangelogEntry> coreChangelog) =>
    [
        .. coreChangelog.Select(entry => new ChangelogFeedItem(null, entry))
            .Concat(heroes.SelectMany(h => h.Config.Changelog
                .Select(entry => new ChangelogFeedItem(h.Hero, entry))))
            .OrderByDescending(item => item.Entry.Date),
    ];
}
