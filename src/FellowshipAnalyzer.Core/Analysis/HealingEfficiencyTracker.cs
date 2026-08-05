using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Per-spell healing throughput measured against the two things a healer actually spends on it:
/// resource and time on the global cooldown. Every figure here is that spell's own absolute -
/// healing per point of resource, healing per second of execution time - and nothing is normalized
/// against the best spell in the log.
/// <para>
/// Logs that carry no per-cast cost need a hero override of <see cref="ResourceCostOf"/>; the default
/// reads the cost the spell registry carries, which is empty for a hero whose costs the game data
/// does not publish per ability.
/// </para>
/// <para>
/// A heal that arrives under an effect id is folded onto the ability that owns it, so long as the
/// hero's spellbook names the effect in the entry's <see cref="SpellbookAbility.AdditionalSpells"/>.
/// Without that the casts and the healing land in separate rows and both rates read zero: the cast
/// row has cost and no healing, the effect row has healing and no cast.
/// </para>
/// </summary>
public partial class HealingEfficiencyTracker : Analyzer
{
    private readonly Dictionary<int, SpellCapture> _bySpell = [];

    private Abilities? _abilities;

    private Computed Result => field ??= Compute();

    /// <summary>The resource these efficiency figures are denominated in.</summary>
    protected virtual ResourceTypes Resource => ResourceTypes.Mana;

    /// <summary>Every spell that healed or cost resource, heaviest effective healing first.</summary>
    public IReadOnlyList<SpellHealingEfficiency> BySpell => Result.Spells;

    /// <summary>Effective healing across every tracked spell.</summary>
    public long TotalEffective => Result.Effective;

    /// <summary>Overheal across every tracked spell.</summary>
    public long TotalOverheal => Result.Overheal;

    /// <summary>Resource spent across every tracked spell.</summary>
    public int TotalResourceSpent => Result.Spent;

    /// <summary>Milliseconds of execution time across every tracked spell.</summary>
    public int TotalExecutionMs => Result.ExecutionMs;

    /// <summary>Effective healing per point of resource across every tracked spell.</summary>
    public double HealingPerResource => Result.Spent > 0 ? (double)Result.Effective / Result.Spent : 0;

    /// <summary>Share (0-1) of all tracked healing that was overheal.</summary>
    public double OverhealShare =>
        Result.Effective + Result.Overheal > 0
            ? Result.Overheal / (double)(Result.Effective + Result.Overheal)
            : 0;

    /// <summary>The efficiency row for <paramref name="spellId"/>, or <c>null</c> when it never healed or cost anything.</summary>
    public SpellHealingEfficiency? For(int spellId) =>
        Result.Spells.FirstOrDefault(spell => spell.SpellId == spellId);

    /// <summary>
    /// What <paramref name="castEvent"/> cost in <see cref="Resource"/>. The default takes the cost
    /// the spell registry carries; override when the game data publishes costs the registry does not.
    /// </summary>
    protected virtual int ResourceCostOf(CastEvent castEvent) =>
        Common.Spells.SpellRegistry.MaybeGet(castEvent.Ability.Id)?.Cost(Resource) ?? 0;

    /// <summary>
    /// How long <paramref name="castEvent"/> occupied, in milliseconds. The default uses the global
    /// cooldown the cast triggered, falling back to the spell's cast duration when it triggered none.
    /// </summary>
    protected virtual int ExecutionMsOf(CastEvent castEvent)
    {
        if (castEvent.GlobalCooldown is { Duration: > 0 } gcd) return gcd.Duration;

        var castDuration = Common.Spells.SpellRegistry.MaybeGet(castEvent.Ability.Id)?.CastDuration;
        return castDuration is > 0 ? (int)(castDuration.Value * 1000) : 0;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        var capture = CaptureFor(castEvent.Ability.Id, castEvent.Ability.Name);
        capture.Casts++;
        capture.ResourceSpent += ResourceCostOf(castEvent);
        capture.ExecutionMs += ExecutionMsOf(castEvent);
    }

    [On<HealEvent>(By = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        var capture = CaptureFor(healEvent.Ability.Id, healEvent.Ability.Name);
        capture.Heals++;
        capture.Effective += healEvent.Amount;
        capture.Overheal += healEvent.Overheal ?? 0;
    }

    private SpellCapture CaptureFor(int spellId, string? name)
    {
        var owner = OwningAbility(spellId);
        var id = owner?.PrimarySpell.FSLID ?? spellId;
        var label = owner is not null ? owner.Name ?? owner.PrimarySpell.Name : name;

        if (!_bySpell.TryGetValue(id, out var capture))
            _bySpell[id] = capture = new SpellCapture { Name = label ?? string.Empty };

        if (string.IsNullOrEmpty(capture.Name) && !string.IsNullOrEmpty(label))
            capture.Name = label;

        return capture;
    }

    private SpellbookAbility? OwningAbility(int spellId) =>
        (_abilities ??= Owner.GetModule<Abilities>())?.GetAbility(spellId);

    private Computed Compute()
    {
        long effective = 0, overheal = 0;
        var spent = 0;
        var executionMs = 0;

        var spells = new List<SpellHealingEfficiency>(_bySpell.Count);
        foreach (var (id, capture) in _bySpell)
        {
            effective += capture.Effective;
            overheal += capture.Overheal;
            spent += capture.ResourceSpent;
            executionMs += capture.ExecutionMs;

            spells.Add(new SpellHealingEfficiency(
                id,
                capture.Name,
                capture.Casts,
                capture.Heals,
                capture.Effective,
                capture.Overheal,
                capture.ResourceSpent,
                capture.ExecutionMs));
        }

        spells.Sort(static (left, right) => right.Effective.CompareTo(left.Effective));
        return new Computed(spells, effective, overheal, spent, executionMs);
    }

    private sealed class SpellCapture
    {
        public string Name { get; set; } = string.Empty;
        public int Casts { get; set; }
        public int Heals { get; set; }
        public long Effective { get; set; }
        public long Overheal { get; set; }
        public int ResourceSpent { get; set; }
        public int ExecutionMs { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<SpellHealingEfficiency> Spells,
        long Effective,
        long Overheal,
        int Spent,
        int ExecutionMs);
}

/// <summary>
/// One spell's healing measured against what it cost to press.
/// </summary>
/// <param name="SpellId">The spell's id.</param>
/// <param name="Name">Its name as the log reported it.</param>
/// <param name="Casts">Casts the player made.</param>
/// <param name="Heals">Heal events the spell produced, ticks included.</param>
/// <param name="Effective">Healing that landed.</param>
/// <param name="Overheal">Healing that was lost to full health bars.</param>
/// <param name="ResourceSpent">Resource the casts cost.</param>
/// <param name="ExecutionMs">Milliseconds of global cooldown or cast time the casts occupied.</param>
public sealed record SpellHealingEfficiency(
    int SpellId,
    string Name,
    int Casts,
    int Heals,
    long Effective,
    long Overheal,
    int ResourceSpent,
    int ExecutionMs)
{
    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Effective + Overheal;

    /// <summary>Share (0-1) of this spell's healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;

    /// <summary>Effective healing per point of resource spent, or zero when the spell costs nothing.</summary>
    public double HealingPerResource => ResourceSpent > 0 ? (double)Effective / ResourceSpent : 0;

    /// <summary>Effective healing per second of execution time, or zero when the spell occupies none.</summary>
    public double HealingPerExecutionSecond => ExecutionMs > 0 ? Effective / (ExecutionMs / 1000d) : 0;
}
