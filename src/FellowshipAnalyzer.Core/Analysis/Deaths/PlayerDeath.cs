using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Deaths;

/// <summary>
/// One death of the analyzed player, with the lead-up window <see cref="DeathTracker"/> captured around
/// it: every hit the player took, the healing that landed on them, and how each of their defensive and
/// healing abilities was placed at the instant they died.
/// <para>
/// Prevention follows the same accounting as <see cref="DamageTakenTracker"/>: <see cref="Prevented"/> is
/// <see cref="Mitigated"/> plus <see cref="Absorbed"/>, and <see cref="Blocked"/> is a labelled share of
/// <see cref="Mitigated"/> reported alongside rather than a third bucket.
/// </para>
/// </summary>
public sealed class PlayerDeath
{
    /// <summary>When the player died.</summary>
    public required int Timestamp { get; init; }

    /// <summary>The pull the death happened in, or <c>null</c> when it fell outside every pull window.</summary>
    public required Pull? Pull { get; init; }

    /// <summary>The ability the log names on the death event, as resolved from the report's master data.</summary>
    public required Ability KillingBlow { get; init; }

    /// <summary>The actor id the log credits with the killing blow.</summary>
    public required int KillerId { get; init; }

    /// <summary>The start of the captured window: <see cref="DeathTracker.RecapWindowMs"/> back from <see cref="Timestamp"/>, clamped to the pull's start.</summary>
    public required int WindowStart { get; init; }

    /// <summary>Every hit the player took inside the window, in the order they landed.</summary>
    public required IReadOnlyList<IncomingHit> Hits { get; init; }

    /// <summary>Effective healing that landed on the player inside the window, overheal excluded.</summary>
    public required long HealingReceived { get; init; }

    /// <summary>Every defensive and healing ability in the player's spellbook, as each was placed at the death.</summary>
    public required IReadOnlyList<DefensiveReadiness> Defensives { get; init; }

    /// <summary>The player's maximum hit points as the window's hits reported them, or <c>null</c> when no hit carried a snapshot.</summary>
    public required long? MaxHitPoints { get; init; }

    private Computed Result => field ??= Compute();

    /// <summary>How long the captured window covers, in milliseconds.</summary>
    public int WindowDurationMs => Timestamp - WindowStart;

    /// <summary>The raw incoming damage the window's hits carried, before any mitigation.</summary>
    public long DamageFaced => Result.Unmitigated;

    /// <summary>The damage that actually landed on the player during the window.</summary>
    public long DamageTaken => Result.Taken;

    /// <summary>Damage the window's hits carried that never landed.</summary>
    public long Prevented => Result.Mitigated + Result.Absorbed;

    /// <summary>The mitigated portion of <see cref="Prevented"/>.</summary>
    public long Mitigated => Result.Mitigated;

    /// <summary>The absorbed portion of <see cref="Prevented"/>.</summary>
    public long Absorbed => Result.Absorbed;

    /// <summary>The share of <see cref="Mitigated"/> the log attributes to blocks.</summary>
    public long Blocked => Result.Blocked;

    /// <summary>Hits the player took during the window.</summary>
    public int HitCount => Hits.Count;

    /// <summary>How many of <see cref="HitCount"/> came in as a block.</summary>
    public int BlockedHits => Result.BlockedHits;

    /// <summary>Share (0-1) of <see cref="DamageFaced"/> that never landed.</summary>
    public double PreventedShare => DamageFaced > 0 ? Math.Clamp(Prevented / (double)DamageFaced, 0, 1) : 0;

    /// <summary>Every ability that hit the player during the window, heaviest first by damage that landed.</summary>
    public IReadOnlyList<DamageTakenSource> BySource => Result.Sources;

    /// <summary>
    /// The player's hit points as the window opened, reconstructed from the first hit's own snapshot by
    /// adding back the damage that hit removed. <c>null</c> when no hit inside the window carried one.
    /// </summary>
    public long? HitPointsAtWindowStart => Result.HitPointsAtWindowStart;

    /// <summary>Abilities the player cast during the window.</summary>
    public IReadOnlyList<DefensiveReadiness> Pressed => Result.Pressed;

    /// <summary>Abilities whose effect was active on the player at the moment they died.</summary>
    public IReadOnlyList<DefensiveReadiness> ActiveAtDeath => Result.ActiveAtDeath;

    /// <summary>
    /// Abilities that held a charge at the death, were not cast during the window, and had no effect
    /// active on the player. A log carries no reason an ability went uncast, so this reports availability
    /// and nothing more.
    /// </summary>
    public IReadOnlyList<DefensiveReadiness> AvailableUnused => Result.AvailableUnused;

    private Computed Compute()
    {
        long taken = 0, unmitigated = 0, mitigated = 0, absorbed = 0, blocked = 0;
        var blockedHits = 0;

        var captures = new Dictionary<int, SourceCapture>();
        foreach (var hit in Hits)
        {
            taken += hit.Amount;
            unmitigated += hit.Unmitigated;
            mitigated += hit.Mitigated;
            absorbed += hit.Absorbed;
            blocked += hit.Blocked;
            if (hit.HitType == HitType.Block) blockedHits++;

            var id = hit.Ability.Id;
            if (!captures.TryGetValue(id, out var capture))
                captures[id] = capture = new SourceCapture { Name = hit.Ability.Name };

            if (string.IsNullOrEmpty(capture.Name) && !string.IsNullOrEmpty(hit.Ability.Name))
                capture.Name = hit.Ability.Name;

            capture.Hits++;
            capture.Taken += hit.Amount;
            capture.Unmitigated += hit.Unmitigated;
            capture.Mitigated += hit.Mitigated;
            capture.Absorbed += hit.Absorbed;
            capture.Blocked += hit.Blocked;
            if (hit.HitType == HitType.Block) capture.BlockedHits++;
        }

        var seconds = WindowDurationMs / 1000d;
        var sources = new List<DamageTakenSource>(captures.Count);
        foreach (var (id, capture) in captures)
        {
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

        long? hitPointsAtWindowStart = null;
        foreach (var hit in Hits)
        {
            if (hit.HitPointsAfter is not { } after) continue;
            hitPointsAtWindowStart = after + hit.Amount;
            break;
        }

        List<DefensiveReadiness> pressed = [], activeAtDeath = [], availableUnused = [];
        foreach (var defensive in Defensives)
        {
            if (defensive.Pressed) pressed.Add(defensive);
            if (defensive.ActiveAtDeath) activeAtDeath.Add(defensive);
            if (defensive.Unpressed) availableUnused.Add(defensive);
        }

        return new Computed(
            sources, taken, unmitigated, mitigated, absorbed, blocked, blockedHits,
            hitPointsAtWindowStart, pressed, activeAtDeath, availableUnused);
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
        int BlockedHits,
        long? HitPointsAtWindowStart,
        IReadOnlyList<DefensiveReadiness> Pressed,
        IReadOnlyList<DefensiveReadiness> ActiveAtDeath,
        IReadOnlyList<DefensiveReadiness> AvailableUnused);
}

/// <summary>
/// One hit the player took, with the log's own accounting of what the hit carried and what reached them.
/// </summary>
/// <param name="Timestamp">When the hit landed.</param>
/// <param name="Ability">The ability that dealt it, as resolved from the report's master data.</param>
/// <param name="SourceId">The actor id that dealt it.</param>
/// <param name="Amount">The damage that actually landed.</param>
/// <param name="Unmitigated">The raw incoming damage before any mitigation.</param>
/// <param name="Mitigated">The portion prevented by damage reduction, blocks included.</param>
/// <param name="Absorbed">The portion prevented by absorb shields.</param>
/// <param name="Blocked">The share of <paramref name="Mitigated"/> the log attributes to a block.</param>
/// <param name="HitType">How the log classified the hit.</param>
/// <param name="HitPointsAfter">The player's hit points once the hit had landed, or <c>null</c> when the hit carried no snapshot.</param>
/// <param name="MaxHitPoints">The player's maximum hit points at the hit, or <c>null</c> when the hit carried no snapshot.</param>
/// <param name="AbsorbAfter">The absorb shield left on the player once the hit had landed, or <c>null</c> when the hit carried no snapshot.</param>
public sealed record IncomingHit(
    int Timestamp,
    Ability Ability,
    int SourceId,
    long Amount,
    long Unmitigated,
    long Mitigated,
    long Absorbed,
    long Blocked,
    HitType HitType,
    long? HitPointsAfter,
    long? MaxHitPoints,
    int? AbsorbAfter)
{
    /// <summary>Damage this hit carried that never landed.</summary>
    public long Prevented => Mitigated + Absorbed;

    /// <summary>The curated classification of the ability that dealt this hit, or <c>null</c> when it has not been classified.</summary>
    public EnemyAbility? Classification => EnemyAbilities.MaybeGet(Ability.Id);
}

/// <summary>
/// How one defensive or healing ability was placed at a death: whether the player cast it during the
/// lead-up window, whether its effect was active on them when they died, and how many charges it held.
/// </summary>
/// <param name="Ability">The spellbook entry this describes.</param>
/// <param name="ChargesAvailable">Charges the ability held at the death.</param>
/// <param name="CastsInWindow">Times the player cast it inside the window.</param>
/// <param name="LastCastTimestamp">When the player last cast it inside the window, or <c>null</c> when they did not.</param>
/// <param name="ActiveAtDeath">Whether one of the ability's own auras was active on the player at the death.</param>
public sealed record DefensiveReadiness(
    SpellbookAbility Ability,
    int ChargesAvailable,
    int CastsInWindow,
    int? LastCastTimestamp,
    bool ActiveAtDeath)
{
    /// <summary>Whether the player cast the ability inside the window.</summary>
    public bool Pressed => CastsInWindow > 0;

    /// <summary>Whether the ability held a charge at the death yet neither was cast in the window nor had an aura active.</summary>
    public bool Unpressed => CastsInWindow == 0 && !ActiveAtDeath && ChargesAvailable > 0;
}
