using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The read surface every Oblivion measurement is exposed on.</summary>
public interface IOblivionAnalyzer : IAnalyzerSurface;

/// <summary>
/// One Oblivion cast: the Oblivion's Embrace shields it laid on the party, what paid for it, and the
/// tank's Stagger at the moment it went out.
/// </summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="TargetId">The enemy the cast named.</param>
/// <param name="AlliesShielded">Party members that took a shield from this cast.</param>
/// <param name="ShieldApplied">Absorb the shields carried when applied, summed across those allies.</param>
/// <param name="LargestSingleAbsorb">The largest share of one incoming hit any of those shields took.</param>
/// <param name="FreeCastSource">What paid for this cast, or null when the cast cost resources.</param>
/// <param name="TankStaggerFraction">The tank's pending Stagger as a fraction of its maximum hit points immediately before the cast, or null when no reading within <see cref="StaggerTracker.ReadingMaxAgeMs"/> supports it.</param>
/// <param name="CleanseAvailable">Whether Amend Fate or Restore Continuity had a charge ready at the cast, and the tank was alive to receive it.</param>
public sealed record OblivionCast(
    int Timestamp,
    int TargetId,
    int AlliesShielded,
    long ShieldApplied,
    long LargestSingleAbsorb,
    FreeCastSource? FreeCastSource,
    double? TankStaggerFraction,
    bool CleanseAvailable)
{
    /// <summary>Whether Fellowship logged this cast as costing no resources.</summary>
    public bool WasFree => FreeCastSource is not null;

    /// <summary>
    /// Whether this cast is the doc's priority error: the tank held more pending Stagger than
    /// <see cref="OblivionAnalyzer.CleansePriorityStaggerFraction"/> of its maximum hit points and a
    /// cleanse was there to take it off. A cast with no reading is not flagged.
    /// </summary>
    public bool AtCleansePriority =>
        TankStaggerFraction > OblivionAnalyzer.CleansePriorityStaggerFraction && CleanseAvailable;

    /// <summary>Whether the doc's priority rule could judge this cast at all.</summary>
    public bool Judged => TankStaggerFraction is not null;
}

/// <summary>
/// Measures every Oblivion cast: the Oblivion's Embrace shielding it put on the party, whether a free
/// cast paid for it, and the tank's pending Stagger at the moment it went out.
/// <para>
/// One cast shields several party members at once, so the shield ledger inherited from
/// <see cref="AbsorbAnalyzer"/> keeps a separate reading per ally and <see cref="Casts"/> regroups those
/// readings under the cast that laid them, by taking the player's most recent Oblivion cast at or before
/// each shield was applied. A shield laid by a cast in an earlier pull counts in the pull totals and in no
/// cast's row.
/// </para>
/// <para>
/// The shielding is Oblivion's Embrace, so it exists only in a build carrying that talent.
/// <see cref="OblivionsEmbraceTalented"/> says whether the build carries it, and every shield total reads
/// null rather than zero without it.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<StaggerTracker>]
[Dependency<FreeCastTracker>]
[Dependency<SpellUsable>]
public sealed partial class OblivionAnalyzer : AbsorbAnalyzer, IOblivionAnalyzer
{
    /// <summary>
    /// The share of a tank's maximum hit points of pending Stagger past which cleansing it outranks
    /// casting Oblivion.
    /// </summary>
    public const double CleansePriorityStaggerFraction = 0.40;

    private readonly List<CastRecord> _casts = [];

    private CastLedger Ledger => field ??= BuildLedger();

    /// <inheritdoc/>
    /// <remarks>
    /// Every Oblivion cast lays a fresh shield, so one arriving on an ally who still carries one is a
    /// separate reading with its own absorb.
    /// </remarks>
    protected override bool ReapplicationOpensNewAbsorb => true;

    /// <summary>Every Oblivion cast this pull, in cast order.</summary>
    public IReadOnlyList<OblivionCast> Casts => Ledger.Casts;

    /// <summary>Oblivion casts this pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>Whether the build carries Oblivion's Embrace, which is what makes Oblivion shield the party.</summary>
    public bool OblivionsEmbraceTalented =>
        Owner.SelectedCombatant.HasTalent(AeonaTalents.OblivionsEmbrace);

    /// <summary>
    /// Absorb the shields carried when applied, summed over the pull. Null without Oblivion's
    /// Embrace, where the shield does not exist.
    /// </summary>
    public long? ShieldApplied => OblivionsEmbraceTalented ? Ledger.Applied : null;

    /// <summary>
    /// Absorb the shields carried per Oblivion cast, averaged over the pull. Null without Oblivion's
    /// Embrace, or with no cast to average over.
    /// </summary>
    public double? ShieldAppliedPerCast =>
        OblivionsEmbraceTalented && _casts.Count > 0 ? (double)Ledger.Applied / _casts.Count : null;

    /// <summary>
    /// The largest share of one incoming hit any Oblivion's Embrace shield took this pull, which is the
    /// most health the shield saved a party member in a single hit. Null without Oblivion's Embrace.
    /// </summary>
    public long? LargestSingleAbsorb => OblivionsEmbraceTalented ? Ledger.LargestSingleAbsorb : null;

    /// <summary>Oblivion casts Fellowship logged as costing no resources.</summary>
    public int FreeCasts => Ledger.Casts.Count(cast => cast.WasFree);

    /// <summary>
    /// Chances to make a free cast the pull offered: every Uchronia window and every Epoch Break window
    /// that opened inside it, whichever ability each was eventually spent on.
    /// </summary>
    public int FreeCastOpportunities => FreeCastTracker.OpportunitiesBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>
    /// Oblivion casts made while the tank held more pending Stagger than
    /// <see cref="CleansePriorityStaggerFraction"/> of its maximum hit points and a cleanse was ready.
    /// Read it against <see cref="CastsJudged"/>.
    /// </summary>
    public int CastsAtCleansePriority => Ledger.Casts.Count(cast => cast.AtCleansePriority);

    /// <summary>Oblivion casts the doc's priority rule could judge.</summary>
    public int CastsJudged => Ledger.Casts.Count(cast => cast.Judged);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnOblivionCast(CastEvent e) => RecordCast(e.Timestamp, e.TargetId);

    [On<FreeCastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnFreeOblivionCast(FreeCastEvent e) => RecordCast(e.Timestamp, e.TargetId);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldApplied(ApplyBuffEvent e) => OpenShield(e, e.Absorb ?? 0);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldRefreshed(RefreshBuffEvent e) => OpenShield(e, e.Absorb ?? 0);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldRemoved(RemoveBuffEvent e) => CloseShield(e);

    [On<AbsorbedEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldAbsorbed(AbsorbedEvent e) => RecordAbsorbed(e);

    /// <summary>
    /// Records the cast with the cleanse opportunity sampled now, because <see cref="SpellUsable"/>
    /// answers for the dispatch instant alone. The cast itself proves Oblivion was castable, so the only
    /// remaining condition the doc names is that a cleanse was ready and the tank alive to take it.
    /// </summary>
    private void RecordCast(int timestamp, int targetId)
    {
        var tankAlive = StaggerTracker.TankId is { } tank && StaggerTracker.IsAlive(tank, timestamp);
        var cleanseReady = SpellUsable.IsAvailable(Spells.AmendFate.FSLID)
            || SpellUsable.IsAvailable(Spells.RestoreContinuity.FSLID);

        _casts.Add(new CastRecord(timestamp, targetId, tankAlive && cleanseReady));
    }

    private CastLedger BuildLedger()
    {
        var shieldsByCast = new List<AbsorbUse>[_casts.Count];
        for (var i = 0; i < shieldsByCast.Length; i++)
            shieldsByCast[i] = [];

        long applied = 0, largestSingleAbsorb = 0;
        var castIndex = -1;

        foreach (var shield in Absorbs)
        {
            applied += shield.Total;

            foreach (var hit in shield.Hits)
            {
                if (hit.Amount > largestSingleAbsorb) largestSingleAbsorb = hit.Amount;
            }

            while (castIndex + 1 < _casts.Count && _casts[castIndex + 1].Timestamp <= shield.Start)
                castIndex++;

            if (castIndex >= 0) shieldsByCast[castIndex].Add(shield);
        }

        var tankId = StaggerTracker.TankId;
        var casts = new List<OblivionCast>(_casts.Count);

        for (var i = 0; i < _casts.Count; i++)
        {
            var record = _casts[i];
            var shields = shieldsByCast[i];

            long castApplied = 0, castLargest = 0;

            foreach (var shield in shields)
            {
                castApplied += shield.Total;

                foreach (var hit in shield.Hits)
                {
                    if (hit.Amount > castLargest) castLargest = hit.Amount;
                }
            }

            casts.Add(new OblivionCast(
                record.Timestamp,
                record.TargetId,
                shields.Count,
                castApplied,
                castLargest,
                FreeCastTracker.FreeCastAt(record.Timestamp, Spells.Oblivion.FSLID)?.Source,
                tankId is { } tank
                    ? StaggerTracker.StaggerFractionOfMaxHp(tank, record.Timestamp, StaggerTracker.ReadingMaxAgeMs)
                    : null,
                record.CleanseAvailable));
        }

        return new CastLedger(casts, applied, largestSingleAbsorb);
    }

    private readonly record struct CastRecord(int Timestamp, int TargetId, bool CleanseAvailable);

    private sealed record CastLedger(
        List<OblivionCast> Casts,
        long Applied,
        long LargestSingleAbsorb);
}
