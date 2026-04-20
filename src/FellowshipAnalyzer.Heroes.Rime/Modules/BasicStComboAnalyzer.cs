using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/**
 * While Bursting Ice is active on an enemy you gain Winter's Embrace, causing you to deal 20% more damage.
 * 
 * Winter's Embrace does not affect Bursting Ice.
 */
public sealed class BasicStComboAnalyzer : Analyzer
{
    private const int WintersEmbraceDurationMs = 3000;
    private const double WintersEmbraceIncrease = 0.20;

    // ── Damage tracking for Winter's Embrace ──────────────────────────────────

    private bool _wintersEmbraceActive;
    private long _totalBonusDamage;
    private int _buffedDamageEventCount;
    private readonly Dictionary<int, (string Name, long Damage)> _bonusDamageBySpell = [];

    // ── Public accessors ──────────────────────────────────────────────────────

    /// <summary>Total effective damage attributable to the Winter's Embrace 20% buff.</summary>
    public long TotalBonusDamage => _totalBonusDamage;

    /// <summary>Number of damage events that occurred while Winter's Embrace was active (excluding Bursting Ice).</summary>
    public int BuffedDamageEventCount => _buffedDamageEventCount;

    /// <summary>Per-spell breakdown of effective bonus damage from Winter's Embrace, keyed by ability game ID.</summary>
    public IReadOnlyDictionary<int, (string Name, long Damage)> BonusDamageBySpell => _bonusDamageBySpell;

    // ── Score card & window results ───────────────────────────────────────────

    public AnalyzerScoreCard ScoreCard { get; private set; } = null!;
    public int EvaluatedWindows { get; private set; }
    public int SuccessfulWindows { get; private set; }
    public int PartialWindows { get; private set; }
    public int IgnoredAoeWindows { get; private set; }
    public IReadOnlyList<StComboWindowEvaluation> Windows { get; private set; } = [];
    public IReadOnlyList<RimeAnalyzerFinding> Findings { get; private set; } = [];

    public override void Initialize()
    {
        AddEventListener(Events.ApplyBuff.By(SELECTED_PLAYER).Spell(RimeSpells.WintersBlessingBuff), OnWintersBlessingApplied);
        AddEventListener(Events.RemoveBuff.By(SELECTED_PLAYER).Spell(RimeSpells.WintersBlessingBuff), OnWintersBlessingRemoved);
        AddEventListener(Events.Damage.By(SELECTED_PLAYER), OnDamage);
    }   

    private void OnWintersBlessingApplied(ApplyBuffEvent @event)
    {
        _wintersEmbraceActive = true;
    }

    private void OnWintersBlessingRemoved(RemoveBuffEvent @event)
    {
        _wintersEmbraceActive = false;
    }

    private void OnDamage(DamageEvent damageEvent)
    {
        if (!_wintersEmbraceActive)
            return;

        // Winter's Embrace does not affect Bursting Ice itself
        if (damageEvent.Ability.Id == RimeSpells.BurstingIce.Guid ||
            damageEvent.Ability.Id == RimeSpells.BurstingIceDamage.Guid)
            return;

        var bonus = CombatMath.CalculateEffectiveDamage(damageEvent, WintersEmbraceIncrease);
        _totalBonusDamage += bonus;
        _buffedDamageEventCount++;

        var id = damageEvent.Ability.Id;
        var name = damageEvent.Ability.Name;
        _bonusDamageBySpell[id] = _bonusDamageBySpell.TryGetValue(id, out var existing)
            ? (existing.Name, existing.Damage + bonus)
            : (name, bonus);
    }


    public override void Complete()
    {
        var casts = Owner.GetModule<SpellUsable>()?.Casts ?? [];

        var evaluations = new List<StComboWindowEvaluation>();
        var findings = new List<RimeAnalyzerFinding>();
        var ignoredAoeWindows = 0;

        foreach (var cast in casts)
        {
            if (cast.Id != RimeSpells.BurstingIce.Guid)
                continue;

            var startTimestamp = cast.Timestamp;
            var endTimestamp = startTimestamp + WintersEmbraceDurationMs;
            var castsInWindow = casts
                .Where(c => c.Timestamp > startTimestamp && c.Timestamp <= endTimestamp)
                .ToList();

            var relevant = castsInWindow
                .Where(c =>
                    c.Id == RimeSpells.GlacialBlast.Id ||
                    c.Id == RimeSpells.ColdSnap.Id ||
                    c.Id == RimeSpells.FreezingTorrent.Id ||
                    c.Id == RimeSpells.IceComet.Id)
                .ToList();

            if (relevant.Count > 0 && relevant[0].Id == RimeSpells.IceComet.Id)
            {
                ignoredAoeWindows += 1;
                continue;
            }

            var glacialBlastIndex = relevant.FindIndex(c => c.Id == RimeSpells.GlacialBlast.Id);
            var finisherIndex = relevant.FindIndex(c =>
                c.Id == RimeSpells.ColdSnap.Id || c.Id == RimeSpells.FreezingTorrent.Id);

            var successful = glacialBlastIndex == 0 && finisherIndex == 1;
            var partial = glacialBlastIndex == 0 && !successful;
            var outcome = successful
                ? "Executed Bursting Ice -> Glacial Blast -> finisher inside Winter's Embrace."
                : partial
                    ? finisherIndex > 1
                        ? "Opened with Glacial Blast, but the finisher landed late or after an extra cast."
                        : "Opened with Glacial Blast, but the Winter's Embrace window did not also fit Cold Snap or Freezing Torrent."
                    : glacialBlastIndex > 0
                        ? "Glacial Blast was delayed behind another cast."
                        : "No Glacial Blast was fitted into the Winter's Embrace window.";

            evaluations.Add(new StComboWindowEvaluation(
                startTimestamp,
                endTimestamp,
                cast.TargetId,
                outcome,
                successful,
                partial,
                relevant));
        }

        var evaluatedWindows = evaluations.Count;
        var successfulWindows = evaluations.Count(w => w.Successful);
        var partialWindows = evaluations.Count(w => w.Partial);
        var score = evaluatedWindows == 0
            ? 0
            : (int)Math.Round(((successfulWindows + (partialWindows * 0.5)) / evaluatedWindows) * 100);

        if (evaluatedWindows == 0)
        {
            findings.Add(new RimeAnalyzerFinding("info", "No single-target Bursting Ice windows were detected in the sample."));
        }
        else
        {
            findings.Add(new RimeAnalyzerFinding("info",
                $"{successfulWindows} of {evaluatedWindows} evaluated Bursting Ice windows matched the Basic ST combo."));

            foreach (var failedWindow in evaluations.Where(w => !w.Successful).Take(5))
            {
                findings.Add(new RimeAnalyzerFinding(
                    failedWindow.Partial ? "warning" : "major",
                    failedWindow.Outcome,
                    failedWindow.StartTimestamp));
            }
        }

        var summary = evaluatedWindows == 0
            ? "No ST windows detected in the sample."
            : $"{successfulWindows}/{evaluatedWindows} ST windows cleanly fit Glacial Blast and a finisher inside Winter's Embrace.";

        ScoreCard = new AnalyzerScoreCard("Basic ST Combo", score, summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");
        EvaluatedWindows = evaluatedWindows;
        SuccessfulWindows = successfulWindows;
        PartialWindows = partialWindows;
        IgnoredAoeWindows = ignoredAoeWindows;
        Windows = evaluations;
        Findings = findings;
    }

    public sealed record StComboWindowEvaluation(
        int StartTimestamp,
        int EndTimestamp,
        int TargetId,
        string Outcome,
        bool Successful,
        bool Partial,
        IReadOnlyList<TrackedAbilityCast> CastsInWindow);
}
