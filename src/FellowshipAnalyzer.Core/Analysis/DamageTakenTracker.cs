using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Core.UI.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Fight-lifetime accounting of everything that hit the player, split by the ability that dealt it.
/// <see cref="DamageEvent.Mitigated"/> and <see cref="DamageEvent.Absorbed"/> sum to the damage
/// prevented; <see cref="DamageEvent.Blocked"/> is a labelled portion of the mitigated figure rather
/// than a third bucket, so it is reported alongside and never added in.
/// </summary>
public sealed partial class DamageTakenTracker : EventSubscriber
{
    private readonly Dictionary<int, SourceCapture> _bySource = [];
    private readonly List<DamageTakenHit> _hits = [];

    private Computed Result => field ??= Compute();

    /// <summary>Every hit that landed on the player, in encounter order.</summary>
    public IReadOnlyList<DamageTakenHit> Hits => _hits;

    /// <inheritdoc/>
    public override Type? StatisticsComponentType => TotalTaken > 0 ? typeof(DamageTakenStatistics) : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    /// <summary>Damage that actually landed on the player across the fight.</summary>
    public long TotalTaken => Result.Taken;

    /// <summary>The raw incoming damage before any mitigation.</summary>
    public long TotalFaced => Result.Unmitigated;

    /// <summary>Damage prevented by mitigation and absorbs.</summary>
    public long TotalPrevented => Result.Mitigated + Result.Absorbed;

    /// <summary>The mitigated portion of <see cref="TotalPrevented"/>.</summary>
    public long TotalMitigated => Result.Mitigated;

    /// <summary>The absorbed portion of <see cref="TotalPrevented"/>.</summary>
    public long TotalAbsorbed => Result.Absorbed;

    /// <summary>The share of <see cref="TotalMitigated"/> the log attributes to blocks.</summary>
    public long TotalBlocked => Result.Blocked;

    /// <summary>Hits the player took across the fight.</summary>
    public int TotalHits => Result.Hits;

    /// <summary>How many of <see cref="TotalHits"/> came in as a block.</summary>
    public int TotalBlockedHits => Result.BlockedHits;

    /// <summary>Share (0-1) of the player's damage taken that arrived as a block.</summary>
    public double BlockRate => TotalHits > 0 ? (double)TotalBlockedHits / TotalHits : 0;

    /// <summary>Share (0-1) of <see cref="TotalFaced"/> that never landed.</summary>
    public double PreventedShare => TotalFaced > 0 ? Math.Clamp(TotalPrevented / (double)TotalFaced, 0, 1) : 0;

    /// <summary><see cref="TotalTaken"/> spread over the fight's duration.</summary>
    public double DamageTakenPerSecond => PerSecond(TotalTaken);

    /// <summary><see cref="TotalFaced"/> spread over the fight's duration.</summary>
    public double DamageFacedPerSecond => PerSecond(TotalFaced);

    /// <summary>Every ability that damaged the player, heaviest first by damage that landed.</summary>
    public IReadOnlyList<DamageTakenSource> BySource => Result.Sources;

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent)
    {
        var id = damageEvent.Ability.Id;
        if (!_bySource.TryGetValue(id, out var capture))
            _bySource[id] = capture = new SourceCapture { Name = damageEvent.Ability.Name };

        if (string.IsNullOrEmpty(capture.Name) && !string.IsNullOrEmpty(damageEvent.Ability.Name))
            capture.Name = damageEvent.Ability.Name;

        _hits.Add(new DamageTakenHit(damageEvent.Timestamp, damageEvent.Amount));

        capture.Hits++;
        capture.Taken += damageEvent.Amount;
        capture.Unmitigated += damageEvent.UnmitigatedAmount;
        capture.Mitigated += damageEvent.Mitigated;
        capture.Absorbed += damageEvent.Absorbed ?? 0;
        capture.Blocked += damageEvent.Blocked;

        if (damageEvent.HitType == HitType.Block) capture.BlockedHits++;
    }

    private double PerSecond(long amount)
    {
        var seconds = Owner.FightDurationMs / 1000d;
        return seconds > 0 ? amount / seconds : 0;
    }

    private Computed Compute()
    {
        long taken = 0, unmitigated = 0, mitigated = 0, absorbed = 0, blocked = 0;
        var hits = 0;
        var blockedHits = 0;
        var seconds = Owner.FightDurationMs / 1000d;

        var sources = new List<DamageTakenSource>(_bySource.Count);
        foreach (var (id, capture) in _bySource)
        {
            taken += capture.Taken;
            unmitigated += capture.Unmitigated;
            mitigated += capture.Mitigated;
            absorbed += capture.Absorbed;
            blocked += capture.Blocked;
            hits += capture.Hits;
            blockedHits += capture.BlockedHits;

            sources.Add(new DamageTakenSource(
                id,
                capture.Name,
                capture.Hits,
                capture.BlockedHits,
                capture.Taken,
                capture.Unmitigated,
                capture.Mitigated,
                capture.Absorbed,
                capture.Blocked,
                seconds > 0 ? capture.Taken / seconds : 0));
        }

        sources.Sort(static (left, right) => right.Taken.CompareTo(left.Taken));
        return new Computed(sources, taken, unmitigated, mitigated, absorbed, blocked, hits, blockedHits);
    }

    private sealed class SourceCapture
    {
        public string Name { get; set; } = string.Empty;
        public int Hits { get; set; }
        public int BlockedHits { get; set; }
        public long Taken { get; set; }
        public long Unmitigated { get; set; }
        public long Mitigated { get; set; }
        public long Absorbed { get; set; }
        public long Blocked { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<DamageTakenSource> Sources,
        long Taken,
        long Unmitigated,
        long Mitigated,
        long Absorbed,
        long Blocked,
        int Hits,
        int BlockedHits);
}

/// <summary>
/// One hit the player took.
/// </summary>
/// <param name="Timestamp">When it landed.</param>
/// <param name="Amount">The damage that landed after mitigation and absorbs.</param>
public sealed record DamageTakenHit(int Timestamp, long Amount);

/// <summary>
/// One ability's contribution to the damage the player took.
/// </summary>
/// <param name="AbilityId">The damaging ability's spell id.</param>
/// <param name="Name">The ability's name as the log reported it.</param>
/// <param name="Hits">Hits this ability landed on the player.</param>
/// <param name="BlockedHits">How many of <paramref name="Hits"/> came in as a block.</param>
/// <param name="Taken">Damage that actually landed.</param>
/// <param name="Unmitigated">The raw incoming damage before any mitigation.</param>
/// <param name="Mitigated">The portion prevented by damage reduction, blocks included.</param>
/// <param name="Absorbed">The portion prevented by absorb shields.</param>
/// <param name="Blocked">The share of <paramref name="Mitigated"/> the log attributes to blocks.</param>
/// <param name="DamageTakenPerSecond"><paramref name="Taken"/> spread over the fight's duration.</param>
public sealed record DamageTakenSource(
    int AbilityId,
    string Name,
    int Hits,
    int BlockedHits,
    long Taken,
    long Unmitigated,
    long Mitigated,
    long Absorbed,
    long Blocked,
    double DamageTakenPerSecond)
{
    /// <summary>Damage this ability would have dealt but did not.</summary>
    public long Prevented => Mitigated + Absorbed;

    /// <summary>Share (0-1) of <see cref="Unmitigated"/> that never landed.</summary>
    public double PreventedShare => Unmitigated > 0 ? Math.Clamp(Prevented / (double)Unmitigated, 0, 1) : 0;
}
