using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Components;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// Projects generated pull-analyzer streams into <see cref="PerCastData"/> rows, filling the
/// per-pull grouping (timestamp label via <see cref="CombatLogParser.FormatTimestamp"/>, group name,
/// and the <see cref="PullBanner"/>) so guides supply only the varying <see cref="PerCastRow"/> fields.
/// </summary>
public static class PullAnalyzerGuideExtensions
{
    /// <summary>One row per analyzer (a per-pull aggregate).</summary>
    public static IEnumerable<PerCastData> ToPullRows<T>(
        this List<PullAnalyzer<T>> analyzers,
        CombatLogParser parser,
        Func<T, PullStartEvent, PerCastRow> build) where T : IAnalyzerSurface
    {
        foreach (var entry in analyzers)
            yield return ToPerCastData(parser, entry.Pull, build(entry.Analyzer, entry.Pull));
    }

    /// <summary>One row per inner item, flattening each analyzer's <paramref name="items"/> collection.</summary>
    public static IEnumerable<PerCastData> ToItemRows<T, TItem>(
        this List<PullAnalyzer<T>> analyzers,
        CombatLogParser parser,
        Func<T, IEnumerable<TItem>> items,
        Func<TItem, PullStartEvent, PerCastRow> build) where T : IAnalyzerSurface
    {
        foreach (var entry in analyzers)
            foreach (var item in items(entry.Analyzer))
                yield return ToPerCastData(parser, entry.Pull, build(item, entry.Pull));
    }

    private static PerCastData ToPerCastData(CombatLogParser parser, PullStartEvent pull, PerCastRow row) =>
        new()
        {
            Performance = row.Performance,
            Stats = row.Stats,
            Timestamp = parser.FormatTimestamp(row.Timestamp ?? pull.StartTime),
            Sequence = row.Sequence,
            AdditionalContent = row.AdditionalContent,
            Details = row.Details,
            Tooltip = row.Tooltip,
            Group = pull.Name,
            GroupContent = PullBannerFragment(pull),
        };

    private static RenderFragment PullBannerFragment(PullStartEvent pull) => builder =>
    {
        builder.OpenComponent<PullBanner>(0);
        builder.AddAttribute(1, nameof(PullBanner.Pull), pull);
        builder.CloseComponent();
    };
}
