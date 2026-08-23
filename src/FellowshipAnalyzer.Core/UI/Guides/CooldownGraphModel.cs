using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Timeline;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>What a segment of a cooldown graph lane says about the ability over that segment.</summary>
public enum CooldownGraphState
{
    /// <summary>Every charge was in hand and none was spent, for less than one full recharge.</summary>
    Available,

    /// <summary>
    /// Every charge was in hand and none was spent, for at least as long as one recharge takes, so
    /// a whole extra use fit inside the segment.
    /// </summary>
    Wasted,

    /// <summary>At least one charge was recharging.</summary>
    Recharging,

    /// <summary>The segment fell outside every pull, so it is measured by neither the numerator nor the denominator.</summary>
    OutsidePull,
}

/// <summary>One segment of a cooldown graph lane, in dungeon-relative milliseconds.</summary>
/// <param name="Start">When the segment begins.</param>
/// <param name="End">When the segment ends.</param>
/// <param name="State">What the ability was doing across it.</param>
public readonly record struct CooldownGraphSegment(int Start, int End, CooldownGraphState State)
{
    /// <summary>How long the segment lasts.</summary>
    public int DurationMs => End - Start;
}

/// <summary>
/// One ability's cooldown graph lane over a render window: the segments it is drawn from, the
/// casts that consumed a charge, and how much of the window it was recharging for.
/// </summary>
/// <param name="Ability">The spellbook entry this lane reports on.</param>
/// <param name="Segments">The lane's segments, in order and covering the window with no gaps.</param>
/// <param name="CastTimestamps">When each charge-consuming cast was made, in order.</param>
/// <param name="Casts">Casts made inside a pull.</param>
/// <param name="MaxCasts">
/// How many casts the analyzed time had room for, from the recharge period observed over this
/// report and the ability's charge count. <c>null</c> for an ability with no cooldown.
/// </param>
/// <param name="Efficiency">
/// The share (0-1) of analyzed time at least one charge was recharging. <c>null</c> for an ability
/// with no cooldown.
/// </param>
/// <param name="Performance">
/// Where <paramref name="Efficiency"/> lands against the ability's
/// <see cref="CastEfficiencyInfo"/> thresholds, or the <see cref="CooldownGraphModel"/> defaults where
/// the spellbook sets none. <c>null</c> for an ability with no cooldown.
/// </param>
public sealed record CooldownGraphLane(
    SpellbookAbility Ability,
    List<CooldownGraphSegment> Segments,
    List<int> CastTimestamps,
    int Casts,
    int? MaxCasts,
    double? Efficiency,
    QualitativePerformance? Performance)
{
    /// <summary>Whether every cast the analyzed time had room for was made.</summary>
    public bool ReachedMaxCasts => MaxCasts is { } max && Casts >= max;

    /// <summary>
    /// <see cref="Efficiency"/>, raised to 1 where <see cref="ReachedMaxCasts"/>: an ability
    /// cast as often as the window allowed has nothing left to give, whatever share of it was recharging.
    /// </summary>
    public double? EffectiveEfficiency => Efficiency is not { } measured ? null : ReachedMaxCasts ? 1 : measured;
}

/// <summary>
/// Turns one ability's <see cref="UpdateSpellUsableEvent"/> stream into cooldown graph geometry: when
/// it was recharging, when every charge sat in hand, and where a whole extra use fit into the time it
/// sat there.
/// </summary>
/// <remarks>
/// Recharge geometry comes from <see cref="CooldownLaneModel"/>, so a segment ends when the charge
/// actually came back rather than when it was expected to. Everything the recharges do not cover is
/// time the ability was in hand, which is then cut against the pulls: a segment outside every pull is
/// <see cref="CooldownGraphState.OutsidePull"/> and leaves both the numerator and the denominator
/// alone, and each in-pull segment is judged on its own length so a segment either side of a walk
/// between pulls is never read as one long hold.
/// </remarks>
public static class CooldownGraphModel
{
    /// <summary>The share of analyzed time recharging that an ability reaches <see cref="QualitativePerformance.Perfect"/> at, where its spellbook entry names no target.</summary>
    public const double DefaultRecommendedEfficiency = 0.8;

    /// <summary>How far below the recommended share an ability drops to <see cref="QualitativePerformance.Good"/>.</summary>
    public const double DefaultGoodDownstep = 0.05;

    /// <summary>How far below the recommended share an ability drops to <see cref="QualitativePerformance.Ok"/>.</summary>
    public const double DefaultOkDownstep = 0.15;

    /// <summary>
    /// Builds one lane per entry of <paramref name="abilities"/>, in the order given.
    /// </summary>
    /// <param name="abilities">The abilities to draw a lane for.</param>
    /// <param name="events">The analysis event stream, including the fabricated cooldown updates.</param>
    /// <param name="spellbook">The hero's spellbook, which maps an event's ability id onto its entry.</param>
    /// <param name="playerId">The actor id whose casts and cooldowns are measured.</param>
    /// <param name="pulls">The pulls inside the render window.</param>
    /// <param name="windowStart">The start of the render window, in dungeon-relative milliseconds.</param>
    /// <param name="windowEnd">The end of the render window, in dungeon-relative milliseconds.</param>
    public static List<CooldownGraphLane> Build(
        List<SpellbookAbility> abilities,
        List<Event> events,
        Abilities spellbook,
        int playerId,
        List<PullStartEvent> pulls,
        int windowStart,
        int windowEnd)
    {
        if (abilities.Count == 0 || windowEnd <= windowStart) return [];

        var updates = new Dictionary<int, List<UpdateSpellUsableEvent>>(abilities.Count);
        foreach (var ability in abilities)
            updates[ability.PrimarySpell.FSLID] = [];

        foreach (var e in events)
        {
            if (e is not UpdateSpellUsableEvent update || update.SourceId != playerId) continue;
            if (spellbook.GetAbility(update.Ability.Id) is not { } entry) continue;
            if (!updates.TryGetValue(entry.PrimarySpell.FSLID, out var stream)) continue;

            stream.Add(update);
        }

        var spans = InPullSpans(pulls, windowStart, windowEnd);
        var analyzedMs = 0;
        foreach (var span in spans)
            analyzedMs += span.End - span.Start;

        var lanes = new List<CooldownGraphLane>(abilities.Count);
        foreach (var ability in abilities)
        {
            var stream = updates[ability.PrimarySpell.FSLID];
            stream.Sort(static (left, right) => left.Timestamp.CompareTo(right.Timestamp));
            lanes.Add(BuildLane(ability, stream, spans, analyzedMs, windowStart, windowEnd));
        }

        return lanes;
    }

    private static CooldownGraphLane BuildLane(
        SpellbookAbility ability,
        List<UpdateSpellUsableEvent> updates,
        List<Span> spans,
        int analyzedMs,
        int windowStart,
        int windowEnd)
    {
        var (laneSegments, _) = CooldownLaneModel.Build(updates, windowStart, windowEnd);

        var recharges = new List<Span>();
        var castTimestamps = new List<int>();
        foreach (var segment in laneSegments)
        {
            if (segment.ShowIcon) castTimestamps.Add(segment.IconAt);
            if (segment.End > segment.Start) recharges.Add(new Span(segment.Start, segment.End));
        }

        recharges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        castTimestamps.Sort();

        var casts = 0;
        foreach (var timestamp in castTimestamps)
            if (Contains(spans, timestamp))
                casts++;

        var periodMs = ObservedPeriodMs(updates, spans) ?? (int)(ability.GetCooldown() * 1000);
        var segments = BuildSegments(recharges, spans, periodMs, ability.Charges > 1, windowStart, windowEnd);

        if (periodMs <= 0 || analyzedMs <= 0)
            return new CooldownGraphLane(ability, segments, castTimestamps, casts, null, null, null);

        var rechargingMs = 0;
        foreach (var segment in segments)
            if (segment.State == CooldownGraphState.Recharging)
                rechargingMs += OverlapMs(spans, segment.Start, segment.End);

        var castTimeMs = ability.Charges < 2
            ? (ability.ChannelDuration ?? ability.CastDuration ?? 0) * 1000
            : 0;
        var maxCasts = (int)Math.Floor(analyzedMs / (periodMs + castTimeMs) + ability.Charges - 1) + 1;
        var efficiency = rechargingMs / (double)analyzedMs;

        return new CooldownGraphLane(
            ability,
            segments,
            castTimestamps,
            casts,
            maxCasts,
            efficiency,
            Tier(ability, casts >= maxCasts ? 1 : efficiency));
    }

    private static QualitativePerformance Tier(SpellbookAbility ability, double efficiency)
    {
        var recommended = ability.CastEfficiency?.RecommendedEfficiency ?? DefaultRecommendedEfficiency;
        var good = ability.CastEfficiency?.AverageIssueEfficiency ?? recommended - DefaultGoodDownstep;
        var ok = ability.CastEfficiency?.MajorIssueEfficiency ?? recommended - DefaultOkDownstep;

        return PerformanceTiers.FromThresholds(efficiency, recommended, good, ok);
    }

    private static List<CooldownGraphSegment> BuildSegments(
        List<Span> recharges,
        List<Span> spans,
        int periodMs,
        bool hasCharges,
        int windowStart,
        int windowEnd)
    {
        var segments = new List<CooldownGraphSegment>();
        var cursor = windowStart;

        foreach (var recharge in recharges)
        {
            var start = Math.Max(recharge.Start, cursor);
            var end = Math.Min(recharge.End, windowEnd);
            if (end <= start) continue;

            AddInHand(segments, cursor, start, spans, periodMs, hasCharges);
            segments.Add(new CooldownGraphSegment(start, end, CooldownGraphState.Recharging));
            cursor = end;
        }

        AddInHand(segments, cursor, windowEnd, spans, periodMs, hasCharges);
        return segments;
    }

    private static void AddInHand(
        List<CooldownGraphSegment> segments,
        int start,
        int end,
        List<Span> spans,
        int periodMs,
        bool hasCharges)
    {
        if (end <= start) return;

        var cursor = start;
        foreach (var span in spans)
        {
            var inPullStart = Math.Max(span.Start, start);
            var inPullEnd = Math.Min(span.End, end);
            if (inPullEnd <= inPullStart) continue;

            if (inPullStart > cursor)
                segments.Add(new CooldownGraphSegment(cursor, inPullStart, CooldownGraphState.OutsidePull));

            var wasted = hasCharges || (periodMs > 0 && inPullEnd - inPullStart >= periodMs);
            segments.Add(new CooldownGraphSegment(
                inPullStart,
                inPullEnd,
                wasted ? CooldownGraphState.Wasted : CooldownGraphState.Available));

            cursor = inPullEnd;
        }

        if (cursor < end)
            segments.Add(new CooldownGraphSegment(cursor, end, CooldownGraphState.OutsidePull));
    }

    private static int? ObservedPeriodMs(List<UpdateSpellUsableEvent> updates, List<Span> spans)
    {
        var total = 0;
        var count = 0;
        int? start = null;

        foreach (var update in updates)
        {
            switch (update.UpdateType)
            {
                case UpdateSpellUsableType.BeginCooldown:
                    start = update.Timestamp;
                    break;

                case UpdateSpellUsableType.RestoreCharge:
                    Complete(update.Timestamp);
                    start = update.IsOnCooldown ? update.Timestamp : null;
                    break;

                case UpdateSpellUsableType.EndCooldown:
                    Complete(update.Timestamp);
                    start = null;
                    break;
            }
        }

        return count > 0 ? total / count : null;

        void Complete(int end)
        {
            if (start is not { } began || !WithinOneSpan(spans, began, end)) return;
            total += end - began;
            count++;
        }
    }

    private static List<Span> InPullSpans(List<PullStartEvent> pulls, int windowStart, int windowEnd)
    {
        var spans = new List<Span>(pulls.Count);
        foreach (var pull in pulls)
        {
            var start = Math.Max(pull.StartTime, windowStart);
            var end = Math.Min(pull.EndTime, windowEnd);
            if (end > start) spans.Add(new Span(start, end));
        }

        spans.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        return spans;
    }

    private static bool WithinOneSpan(List<Span> spans, int start, int end)
    {
        foreach (var span in spans)
            if (start >= span.Start && end <= span.End)
                return true;

        return false;
    }

    private static bool Contains(List<Span> spans, int timestamp)
    {
        foreach (var span in spans)
            if (timestamp >= span.Start && timestamp <= span.End)
                return true;

        return false;
    }

    private static int OverlapMs(List<Span> spans, int start, int end)
    {
        var total = 0;
        foreach (var span in spans)
            total += Math.Max(0, Math.Min(end, span.End) - Math.Max(start, span.Start));

        return total;
    }

    private readonly record struct Span(int Start, int End);
}
