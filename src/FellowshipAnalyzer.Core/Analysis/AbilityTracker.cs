using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Dungeon-lifetime per-ability totals of the player's output: completed casts, damage dealt, and
/// healing done, keyed by the ability id the log reported. Every figure is tallied twice - once
/// across the whole dungeon and once for the pull it happened in - so the same ability reads
/// dungeon-wide through <see cref="For(int)"/> or scoped to one encounter through
/// <see cref="For(int, PullStartEvent)"/>.
/// <para>
/// Rows accumulate live as events dispatch: a dungeon-wide row keeps growing until the stream ends,
/// while a pull's rows are complete the moment that pull closes, so a pull analyzer reading its
/// own pull's rows at <see cref="PullEndEvent"/> sees final values. Amounts follow
/// <see cref="ThroughputTracker"/> semantics - damage and healing are the effective amounts, with
/// overheal counted separately.
/// </para>
/// </summary>
public sealed partial class AbilityTracker : Analyzer
{
    private readonly Dictionary<int, TrackedAbility> _bySpell = [];
    private readonly List<TrackedAbility> _spells = [];
    private readonly List<PullLedger> _pullLedgers = [];

    /// <summary>Every ability the player cast, damaged, or healed with across the dungeon, in first-use order.</summary>
    public List<TrackedAbility> BySpell => _spells;

    /// <summary>The dungeon-wide totals for <paramref name="spellId"/>, or <c>null</c> when the player never used it.</summary>
    public TrackedAbility? For(int spellId) => _bySpell.GetValueOrDefault(spellId);

    /// <summary>Every ability the player used during <paramref name="pull"/>, in first-use order.</summary>
    public List<TrackedAbility> During(PullStartEvent pull) => LedgerOf(pull)?.Spells ?? [];

    /// <summary>The totals for <paramref name="spellId"/> scoped to <paramref name="pull"/>, or <c>null</c> when the player never used it there.</summary>
    public TrackedAbility? For(int spellId, PullStartEvent pull) => LedgerOf(pull)?.BySpell.GetValueOrDefault(spellId);

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.Activation) return;

        var (dungeon, pull) = Track(castEvent.Ability);
        dungeon.Casts++;
        if (pull is not null) pull.Casts++;
    }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent)
    {
        var (dungeon, pull) = Track(damageEvent.Ability);
        AddDamage(dungeon, damageEvent);
        if (pull is not null) AddDamage(pull, damageEvent);
    }

    [On<HealEvent>(By = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        var (dungeon, pull) = Track(healEvent.Ability);
        AddHeal(dungeon, healEvent);
        if (pull is not null) AddHeal(pull, healEvent);
    }

    private static void AddDamage(TrackedAbility ability, DamageEvent damageEvent)
    {
        ability.DamageHits++;
        ability.Damage += damageEvent.Amount;
        if (!damageEvent.IsCritical) return;

        ability.CriticalDamageHits++;
        ability.CriticalDamage += damageEvent.Amount;
    }

    private static void AddHeal(TrackedAbility ability, HealEvent healEvent)
    {
        ability.Heals++;
        ability.Healing += healEvent.Amount;
        ability.Overheal += healEvent.Overheal ?? 0;
        if (!healEvent.IsCritical) return;

        ability.CriticalHeals++;
        ability.CriticalHealing += healEvent.Amount;
    }

    private (TrackedAbility Dungeon, TrackedAbility? Pull) Track(Ability ability)
    {
        var dungeon = RowOf(_bySpell, _spells, ability);
        var ledger = OpenLedger();
        return (dungeon, ledger is null ? null : RowOf(ledger.BySpell, ledger.Spells, ability));
    }

    private static TrackedAbility RowOf(Dictionary<int, TrackedAbility> bySpell, List<TrackedAbility> spells, Ability ability)
    {
        if (!bySpell.TryGetValue(ability.Id, out var row))
        {
            bySpell[ability.Id] = row = new TrackedAbility(ability.Id);
            spells.Add(row);
        }

        if (string.IsNullOrEmpty(row.Name) && !string.IsNullOrEmpty(ability.Name))
            row.Name = ability.Name;

        return row;
    }

    private PullLedger? OpenLedger()
    {
        if (Owner.CurrentPull is not { } pull) return null;
        if (_pullLedgers.Count > 0 && ReferenceEquals(_pullLedgers[^1].Pull, pull))
            return _pullLedgers[^1];

        var ledger = new PullLedger(pull);
        _pullLedgers.Add(ledger);
        return ledger;
    }

    private PullLedger? LedgerOf(PullStartEvent pull)
    {
        foreach (var ledger in _pullLedgers)
        {
            if (ReferenceEquals(ledger.Pull, pull)) return ledger;
        }

        return null;
    }

    private sealed class PullLedger(PullStartEvent pull)
    {
        public PullStartEvent Pull { get; } = pull;
        public Dictionary<int, TrackedAbility> BySpell { get; } = [];
        public List<TrackedAbility> Spells { get; } = [];
    }
}

/// <summary>
/// One ability's running totals: what the player cast with it and the damage and healing it produced.
/// </summary>
public sealed class TrackedAbility
{
    internal TrackedAbility(int spellId) => SpellId = spellId;

    /// <summary>The ability's spell id.</summary>
    public int SpellId { get; }

    /// <summary>The ability's name as the log reported it.</summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>Completed casts. A cast-start marker for an ability with a cast time is not counted.</summary>
    public int Casts { get; internal set; }

    /// <summary>Damage events the ability produced, zero-damage results included.</summary>
    public int DamageHits { get; internal set; }

    /// <summary>How many of <see cref="DamageHits"/> were critical.</summary>
    public int CriticalDamageHits { get; internal set; }

    /// <summary>Effective damage the ability dealt.</summary>
    public long Damage { get; internal set; }

    /// <summary>The share of <see cref="Damage"/> dealt by critical hits.</summary>
    public long CriticalDamage { get; internal set; }

    /// <summary>Heal events the ability produced, ticks included.</summary>
    public int Heals { get; internal set; }

    /// <summary>How many of <see cref="Heals"/> were critical.</summary>
    public int CriticalHeals { get; internal set; }

    /// <summary>Effective healing the ability did.</summary>
    public long Healing { get; internal set; }

    /// <summary>The share of <see cref="Healing"/> done by critical heals.</summary>
    public long CriticalHealing { get; internal set; }

    /// <summary>Healing the ability lost to overheal.</summary>
    public long Overheal { get; internal set; }

    /// <summary>Damage and heal events combined.</summary>
    public int Hits => DamageHits + Heals;

    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Healing + Overheal;

    /// <summary>Share (0-1) of this ability's healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;

    /// <summary>Effective damage per completed cast, or zero when it was never cast.</summary>
    public double DamagePerCast => Casts > 0 ? (double)Damage / Casts : 0;

    /// <summary>Effective healing per completed cast, or zero when it was never cast.</summary>
    public double HealingPerCast => Casts > 0 ? (double)Healing / Casts : 0;

    /// <summary>Share (0-1) of <see cref="DamageHits"/> that were critical.</summary>
    public double CriticalDamageRate => DamageHits > 0 ? (double)CriticalDamageHits / DamageHits : 0;

    /// <summary>Share (0-1) of <see cref="Heals"/> that were critical.</summary>
    public double CriticalHealRate => Heals > 0 ? (double)CriticalHeals / Heals : 0;
}
