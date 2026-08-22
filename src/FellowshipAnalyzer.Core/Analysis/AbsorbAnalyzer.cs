using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures what an absorb shield consumed against what was still on it when it came off. The removal
/// event carries the absorb that was left, so the waste is a reading rather than an inference; a
/// shield consumed to nothing carries none.
/// <para>
/// Derive a per-hero analyzer from this, keep <c>[ForPull]</c> and the surface marker interface on the
/// leaf, and declare the shield's own <c>[On&lt;&gt;]</c> handlers there: apply and refresh call
/// <see cref="OpenShield"/>, remove calls <see cref="CloseShield"/>, and
/// <see cref="AbsorbedEvent"/> calls <see cref="RecordAbsorbed"/>. Shields are kept per target, so a
/// healer's shields on four allies are four separate readings and a self barrier is one.
/// </para>
/// </summary>
public abstract class AbsorbAnalyzer : Analyzer
{
    private readonly List<AbsorbCapture> _absorbs = [];
    private readonly Dictionary<UnitKey, AbsorbCapture> _open = [];

    private Computed Result => field ??= Compute();

    /// <summary>
    /// Whether laying a shield on a target that already carries one starts a fresh reading. Shields
    /// that stack as separate applications set this; a shield whose re-application only extends the
    /// window it already has leaves it false, so the window stays one reading and
    /// <see cref="Applications"/> is what runs ahead.
    /// </summary>
    protected virtual bool ReapplicationOpensNewAbsorb => false;

    /// <summary>Every absorb this pull, in encounter order.</summary>
    public IReadOnlyList<AbsorbUse> Absorbs => Result.Absorbs;

    /// <summary>Shield windows this pull.</summary>
    public int AbsorbsApplied => Result.Absorbs.Count;

    /// <summary>
    /// Applications this pull, counting a re-application onto a live shield. This runs ahead of
    /// <see cref="AbsorbsApplied"/> when <see cref="ReapplicationOpensNewAbsorb"/> is false.
    /// </summary>
    public int Applications { get; private set; }

    /// <summary>Damage the shields consumed.</summary>
    public long AbsorbUsed => Result.Used;

    /// <summary>Absorb still on a shield when it came off.</summary>
    public long AbsorbWasted => Result.Wasted;

    /// <summary>Shields that came off still holding absorb.</summary>
    public int AbsorbsExpiredUnused => Result.ExpiredUnspent;

    /// <summary>Share (0-1) of the absorb the shields offered that was consumed rather than left to expire.</summary>
    public double AbsorbEfficiency
    {
        get
        {
            var offered = Result.Used + Result.Wasted;
            return offered > 0 ? Result.Used / (double)offered : 0;
        }
    }

    /// <summary>
    /// Opens a shield on the event's target, carrying the absorb the application put on it. Call from
    /// apply and refresh handlers, passing the event's own <c>Absorb</c> as <paramref name="absorbAmount"/>.
    /// </summary>
    protected void OpenShield<TEvent>(TEvent e, long absorbAmount) where TEvent : Event, IHasTargetWithInstanceEvent
    {
        Applications++;

        var key = AuraWindowLedger.KeyOf(e);
        if (_open.TryGetValue(key, out var running))
        {
            if (!ReapplicationOpensNewAbsorb) return;

            running.End = e.Timestamp;
            running.Reapplied = true;
        }

        var shield = new AbsorbCapture { Target = key, Start = e.Timestamp, End = e.Timestamp, AbsorbAmount = absorbAmount };
        _absorbs.Add(shield);
        _open[key] = shield;
    }

    /// <summary>
    /// Closes the shield on the event's target, recording the absorb the event says was left on it.
    /// Call from remove handlers.
    /// </summary>
    protected void CloseShield(RemoveBuffEvent e)
    {
        if (!_open.Remove(AuraWindowLedger.KeyOf(e), out var shield)) return;

        shield.End = e.Timestamp;
        shield.Remaining = e.Absorb ?? 0;
    }

    /// <summary>Adds the absorbed damage to the shield open on the event's target.</summary>
    protected void RecordAbsorbed(AbsorbedEvent e)
    {
        if (_open.TryGetValue(new UnitKey(e.TargetId, 0), out var shield))
        {
            Consume(shield, e);
            return;
        }

        foreach (var (key, candidate) in _open)
        {
            if (key.ActorId != e.TargetId) continue;

            Consume(candidate, e);
            return;
        }
    }

    private static void Consume(AbsorbCapture shield, AbsorbedEvent e)
    {
        shield.Absorbed += e.Amount;
        shield.Hits.Add(new AbsorbHit(e.Timestamp, e.Amount, e.AttackerId, e.ExtraAbility));
    }

    private Computed Compute()
    {
        var absorbs = new List<AbsorbUse>(_absorbs.Count);
        long used = 0, wasted = 0;
        var expiredUnspent = 0;

        foreach (var capture in _absorbs)
        {
            var truncated = _open.ContainsValue(capture);
            var end = truncated ? Math.Max(capture.End, Pull.EndTime) : capture.End;

            absorbs.Add(new AbsorbUse(
                capture.Target,
                capture.Start,
                end,
                capture.AbsorbAmount,
                capture.Absorbed,
                capture.Remaining,
                capture.Hits,
                truncated,
                capture.Reapplied));

            used += capture.Absorbed;
            wasted += capture.Remaining;
            if (capture.Remaining > 0) expiredUnspent++;
        }

        return new Computed(absorbs, used, wasted, expiredUnspent);
    }

    private sealed class AbsorbCapture
    {
        public UnitKey Target { get; init; }
        public int Start { get; init; }
        public int End { get; set; }
        public long AbsorbAmount { get; init; }
        public long Absorbed { get; set; }
        public long Remaining { get; set; }
        public List<AbsorbHit> Hits { get; } = [];
        public bool Reapplied { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<AbsorbUse> Absorbs,
        long Used,
        long Wasted,
        int ExpiredUnspent);
}

/// <summary>One hit an absorb shield took a share of.</summary>
/// <param name="Timestamp">When the hit was taken.</param>
/// <param name="Amount">Damage this shield removed from the hit. Several shields covering one hit each record their own share.</param>
/// <param name="AttackerId">The unit that dealt the hit, when the event names one.</param>
/// <param name="Ability">The ability that dealt the hit, when the event names one.</param>
public sealed record AbsorbHit(int Timestamp, long Amount, int? AttackerId, Ability? Ability);

/// <summary>One absorb shield and what became of it.</summary>
/// <param name="Target">The unit it was laid on.</param>
/// <param name="Start">When it went up.</param>
/// <param name="End">When it came off, or the pull's end when <paramref name="Truncated"/>.</param>
/// <param name="Total">The absorb the application put on it, as the apply event reported it.</param>
/// <param name="Absorbed">Damage it consumed.</param>
/// <param name="Remaining">Absorb still on it when it came off.</param>
/// <param name="Hits">Every hit it took a share of, in encounter order.</param>
/// <param name="Truncated">Whether the pull ended before the shield did.</param>
/// <param name="Reapplied">Whether a fresh shield replaced this one before it expired.</param>
public sealed record AbsorbUse(
    UnitKey Target,
    int Start,
    int End,
    long Total,
    long Absorbed,
    long Remaining,
    IReadOnlyList<AbsorbHit> Hits,
    bool Truncated,
    bool Reapplied)
{
    /// <summary>How long the absorb was up, in milliseconds.</summary>
    public int DurationMs => End - Start;

    /// <summary>Whether the absorb came off with absorb still on it.</summary>
    public bool ExpiredUnspent => Remaining > 0;

    /// <summary>
    /// Absorption the shield spent, as <see cref="Total"/> less the <see cref="Remaining"/> the
    /// removal reported; null when the pull ended before the shield came off.
    /// </summary>
    public long? Effective => Truncated ? null : Total - Remaining;
}
