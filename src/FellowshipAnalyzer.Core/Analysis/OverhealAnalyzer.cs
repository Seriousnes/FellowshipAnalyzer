using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// How much of each healing ability landed on a bar that was already full. Every reading is that
/// ability's overheal against its own output, never against another ability's - a heal-over-time
/// parked on four allies for a whole pull and a cooldown pressed on a dying tank are not comparable,
/// and the game's own ceiling for each is the same 0%.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class OverhealAnalyzer : Analyzer
{
    private readonly Dictionary<int, HealCapture> _bySpell = [];

    private Computed Result => field ??= Compute();

    /// <summary>Every ability that healed this pull, heaviest total healing first.</summary>
    public IReadOnlyList<AbilityOverheal> BySpell => Result.Spells;

    /// <summary>Healing that landed this pull.</summary>
    public long TotalEffective => Result.Effective;

    /// <summary>Healing lost to full health bars this pull.</summary>
    public long TotalOverheal => Result.Overheal;

    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Result.Effective + Result.Overheal;

    /// <summary>Share (0-1) of this pull's healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Result.Overheal / (double)TotalHealing : 0;

    /// <summary>The overheal row for <paramref name="spellId"/>, or <c>null</c> when it did not heal this pull.</summary>
    public AbilityOverheal? For(int spellId) => Result.Spells.FirstOrDefault(spell => spell.SpellId == spellId);

    [On<HealEvent>(By = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        if (!_bySpell.TryGetValue(healEvent.Ability.Id, out var capture))
            _bySpell[healEvent.Ability.Id] = capture = new HealCapture { Name = healEvent.Ability.Name ?? string.Empty };

        if (string.IsNullOrEmpty(capture.Name) && !string.IsNullOrEmpty(healEvent.Ability.Name))
            capture.Name = healEvent.Ability.Name;

        capture.Heals++;
        capture.Effective += healEvent.Amount;
        capture.Overheal += healEvent.Overheal ?? 0;
    }

    private Computed Compute()
    {
        long effective = 0, overheal = 0;
        var spells = new List<AbilityOverheal>(_bySpell.Count);

        foreach (var (id, capture) in _bySpell)
        {
            effective += capture.Effective;
            overheal += capture.Overheal;
            spells.Add(new AbilityOverheal(id, capture.Name, capture.Heals, capture.Effective, capture.Overheal));
        }

        spells.Sort(static (left, right) => right.TotalHealing.CompareTo(left.TotalHealing));
        return new Computed(spells, effective, overheal);
    }

    private sealed class HealCapture
    {
        public string Name { get; set; } = string.Empty;
        public int Heals { get; set; }
        public long Effective { get; set; }
        public long Overheal { get; set; }
    }

    private sealed record Computed(IReadOnlyList<AbilityOverheal> Spells, long Effective, long Overheal);
}

/// <summary>One ability's healing this pull, split into what landed and what was lost to full bars.</summary>
/// <param name="SpellId">The healing ability.</param>
/// <param name="Name">Its name as the log reported it.</param>
/// <param name="Heals">Heal events it produced, ticks included.</param>
/// <param name="Effective">Healing that landed.</param>
/// <param name="Overheal">Healing lost to full health bars.</param>
public sealed record AbilityOverheal(int SpellId, string Name, int Heals, long Effective, long Overheal)
{
    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Effective + Overheal;

    /// <summary>Share (0-1) of this ability's own healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;
}
