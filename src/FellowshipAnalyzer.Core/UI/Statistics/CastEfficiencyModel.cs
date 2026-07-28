using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.UI.Statistics;

/// <summary>
/// One spellbook ability's cast efficiency over the pulls a report view is showing.
/// </summary>
/// <param name="Ability">The spellbook entry this row reports on.</param>
/// <param name="Casts">Casts recorded inside the analyzed pulls.</param>
/// <param name="CastsPerMinute">Casts divided by the analyzed minutes.</param>
/// <param name="MaxCasts">
/// How many casts the analyzed time had room for, from the ability's observed recharge period and
/// charge count. <c>null</c> for an ability with no cooldown, which has no such ceiling.
/// </param>
/// <param name="Efficiency">
/// The share (0-1) of the analyzed time at least one charge was recharging. <c>null</c> for an
/// ability with no cooldown.
/// </param>
/// <param name="MeetsRecommendation">
/// Whether <paramref name="Efficiency"/> reached the ability's
/// <see cref="CastEfficiencyInfo.RecommendedEfficiency"/>, or the ability hit <paramref name="MaxCasts"/>.
/// <c>null</c> when the spellbook entry asks for no recommendation, which is the case for every
/// ability that leaves <see cref="SpellbookAbility.CastEfficiency"/> unset.
/// </param>
public sealed record AbilityCastEfficiency(
    SpellbookAbility Ability,
    int Casts,
    double CastsPerMinute,
    int? MaxCasts,
    double? Efficiency,
    bool? MeetsRecommendation);

/// <summary>
/// Measures how well each spellbook ability was kept rolling, from the player's cast stream and the
/// <see cref="UpdateSpellUsableEvent"/>s <see cref="SpellUsable"/> fabricates, clipped to a set of pulls.
/// </summary>
public static class CastEfficiencyModel
{
    /// <summary>
    /// Builds a row for every enabled, non-<see cref="SpellCategory.Hidden"/> spellbook ability that was
    /// either cast or has a cooldown, in spellbook order.
    /// </summary>
    /// <param name="abilities">The hero's spellbook.</param>
    /// <param name="events">The analysis event stream, including fabricated events.</param>
    /// <param name="playerId">The actor id whose casts and cooldowns are measured.</param>
    /// <param name="pulls">The pulls to measure over: the whole encounter list, or the single selected pull.</param>
    public static IReadOnlyList<AbilityCastEfficiency> Build(
        Abilities abilities,
        IReadOnlyList<Event> events,
        int playerId,
        IReadOnlyList<Pull> pulls)
    {
        var analyzedMs = 0;
        var lastPullEnd = 0;
        foreach (var pull in pulls)
        {
            analyzedMs += Math.Max(0, pull.EndTime - pull.StartTime);
            lastPullEnd = Math.Max(lastPullEnd, pull.EndTime);
        }

        if (analyzedMs <= 0) return [];

        var castCounts = new Dictionary<int, int>();
        var cooldownUpdates = new Dictionary<int, List<UpdateSpellUsableEvent>>();

        foreach (var e in events)
        {
            switch (e)
            {
                case CastEvent cast when cast.SourceId == playerId && IsInAnyPull(pulls, cast.Timestamp):
                    if (abilities.GetAbility(cast.Ability.Id) is { } castAbility)
                    {
                        int castKey = castAbility.PrimarySpell.FSLID;
                        castCounts[castKey] = castCounts.GetValueOrDefault(castKey) + 1;
                    }
                    break;

                case UpdateSpellUsableEvent update when update.SourceId == playerId:
                    if (abilities.GetAbility(update.Ability.Id) is { } cooldownAbility)
                    {
                        int cooldownKey = cooldownAbility.PrimarySpell.FSLID;
                        if (!cooldownUpdates.TryGetValue(cooldownKey, out var stream))
                            cooldownUpdates[cooldownKey] = stream = [];
                        stream.Add(update);
                    }
                    break;
            }
        }

        var analyzedMinutes = analyzedMs / 60_000d;
        var rows = new List<AbilityCastEfficiency>();

        foreach (var ability in abilities.GetAbilities())
        {
            if (ability.Category == SpellCategory.Hidden) continue;

            int key = ability.PrimarySpell.FSLID;
            var casts = castCounts.GetValueOrDefault(key);
            var cooldownMs = ability.GetCooldown() * 1000;

            if (casts == 0 && cooldownMs <= 0) continue;

            var recharges = BuildRecharges(cooldownUpdates.GetValueOrDefault(key), lastPullEnd);

            double? efficiency = null;
            int? maxCasts = null;

            if (cooldownMs > 0)
            {
                var onCooldownMs = 0;
                var completedMs = 0;
                var completedCount = 0;

                foreach (var recharge in recharges)
                {
                    onCooldownMs += OverlapMs(recharge, pulls);
                    if (!CompletedWithinOnePull(recharge, pulls)) continue;
                    completedMs += recharge.End - recharge.Start;
                    completedCount++;
                }

                efficiency = onCooldownMs / (double)analyzedMs;

                var period = completedCount > 0 ? completedMs / (double)completedCount : cooldownMs;
                var castTimeMs = ability.Charges < 2
                    ? (ability.ChannelDuration ?? ability.CastDuration ?? 0) * 1000
                    : 0;
                maxCasts = (int)Math.Floor((analyzedMs / (period + castTimeMs)) + ability.Charges - 1) + 1;
            }

            rows.Add(new AbilityCastEfficiency(
                ability,
                casts,
                analyzedMinutes > 0 ? casts / analyzedMinutes : 0,
                maxCasts,
                efficiency,
                Recommendation(ability, efficiency, casts, maxCasts)));
        }

        return rows;
    }

    private static bool? Recommendation(SpellbookAbility ability, double? efficiency, int casts, int? maxCasts) =>
        ability.CastEfficiency is { Suggestion: true } target && efficiency is { } measured
            ? measured >= target.RecommendedEfficiency || casts >= maxCasts
            : null;

    private static List<Recharge> BuildRecharges(List<UpdateSpellUsableEvent>? updates, int openRechargeEnd)
    {
        var recharges = new List<Recharge>();
        if (updates is null) return recharges;

        int? start = null;

        foreach (var update in updates.OrderBy(e => e.Timestamp))
        {
            switch (update.UpdateType)
            {
                case UpdateSpellUsableType.BeginCooldown:
                    start = update.Timestamp;
                    break;

                case UpdateSpellUsableType.RestoreCharge:
                    if (start is { } restoreStart)
                        recharges.Add(new Recharge(restoreStart, update.Timestamp, Completed: true));
                    start = update.IsOnCooldown ? update.Timestamp : null;
                    break;

                case UpdateSpellUsableType.EndCooldown:
                    if (start is { } endStart)
                        recharges.Add(new Recharge(endStart, update.Timestamp, Completed: true));
                    start = null;
                    break;
            }
        }

        if (start is { } openStart && openRechargeEnd > openStart)
            recharges.Add(new Recharge(openStart, openRechargeEnd, Completed: false));

        return recharges;
    }

    private static int OverlapMs(Recharge recharge, IReadOnlyList<Pull> pulls)
    {
        var total = 0;
        foreach (var pull in pulls)
            total += Math.Max(0, Math.Min(recharge.End, pull.EndTime) - Math.Max(recharge.Start, pull.StartTime));
        return total;
    }

    private static bool CompletedWithinOnePull(Recharge recharge, IReadOnlyList<Pull> pulls)
    {
        if (!recharge.Completed) return false;

        foreach (var pull in pulls)
            if (recharge.Start >= pull.StartTime && recharge.End <= pull.EndTime)
                return true;

        return false;
    }

    private static bool IsInAnyPull(IReadOnlyList<Pull> pulls, int timestamp)
    {
        foreach (var pull in pulls)
            if (timestamp >= pull.StartTime && timestamp <= pull.EndTime)
                return true;

        return false;
    }

    private readonly record struct Recharge(int Start, int End, bool Completed);
}
