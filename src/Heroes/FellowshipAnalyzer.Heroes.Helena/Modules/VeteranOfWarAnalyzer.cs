using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Measures Veteran of War, the passive that has most of Helena's kit shortening the rest of it.
/// Every cast listed in <see cref="Combos"/> hands its reduction to <see cref="SpellUsable"/>, which
/// reports how much of it landed on a running cooldown and how much was generated against an ability
/// that was already available. The second figure is the waste this analyzer exists to surface, and it
/// is attributed to the ability that generated it.
/// <para>
/// Seconds here are model-derived, not measured: the log records no cooldown-reduction event, so the
/// only way to know whether a reduction landed is to ask the cooldown model whether that ability was
/// running at the time. The figures are therefore only as good as the spellbook's cooldowns and charge
/// counts for the build being analyzed.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class VeteranOfWarAnalyzer : Analyzer
{
    private readonly Dictionary<(int Source, int Target), Contribution> _contributions = [];
    private readonly Dictionary<int, int> _sourceCasts = [];
    private readonly Dictionary<int, int> _castsWhileModelledUnavailable = [];
    private readonly Dictionary<int, int> _modelledCharges = [];

    private bool _ultimateActive;
    private int _punishingStrikesStacks;
    private int _targetCasts;

    /// <summary>
    /// The reduction each cast hands to each of its targets, in seconds, from the Season 3
    /// <c>Constants.CooldownReductionCombo</c> block. The Season 2 block carries 1.5s where this one
    /// carries 2s and 6s where it carries 10s; those are the superseded values.
    /// <para>
    /// The block keys its entries by ability DevName, and the ten-second entry is filed under
    /// <c>MoveToTargetStun</c>, which the same dump's <c>Kit</c> maps to Charge. It is modelled as
    /// Hold the Line on the owner's reading of the live tooltip, which names all four of these
    /// abilities. Shield Throw's cast count on report <c>a:gDf7m3N2wvk96dWP</c> fight 22 backs that:
    /// its 301 casts need about 2,700s of reduction beyond natural recharge, and Hold the Line at ten
    /// seconds is what closes the gap.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CooldownCombo> Combos { get; } =
    [
        new(Spells.MeasuredStrike.FSLID, Spells.ShieldSlam.FSLID, 2000),
        new(Spells.MeasuredStrike.FSLID, Spells.ShieldThrow.FSLID, 2000),
        new(Spells.PowerStrike.FSLID, Spells.ShieldSlam.FSLID, 2000),
        new(Spells.PowerStrike.FSLID, Spells.ShieldThrow.FSLID, 2000),
        new(Spells.ShieldSlam.FSLID, Spells.Shockwave.FSLID, 3000),
        new(Spells.ShieldThrow.FSLID, Spells.Shockwave.FSLID, 3000),
        new(Spells.Shockwave.FSLID, Spells.ShieldsUp.FSLID, 6000),
        new(Spells.HoldTheLine.FSLID, Spells.ShieldSlam.FSLID, 10000),
        new(Spells.HoldTheLine.FSLID, Spells.ShieldThrow.FSLID, 10000),
        new(Spells.HoldTheLine.FSLID, Spells.Shockwave.FSLID, 10000),
        new(Spells.HoldTheLine.FSLID, Spells.ShieldsUp.FSLID, 10000),
    ];

    /// <summary>
    /// The <c>Scalers.ActiveUltimate</c> multiplier: an active Spirit ability doubles every reduction
    /// the combo table generates. The <c>Scalers.SpiritProc</c> multiplier doubles it again, but a
    /// Spirit Refund proc emits nothing the log can be read for, so it is not modelled.
    /// </summary>
    public const double ActiveUltimateScaler = 2.0;

    /// <summary>
    /// Punishing Strikes' <c>BlockToIncreasedPower.CooldownReducementMultiplier</c>: while the proc a
    /// block leaves behind is up, the next reduction is doubled. It multiplies with
    /// <see cref="ActiveUltimateScaler"/> rather than replacing it, so a combo cast under both is worth
    /// four times its table value.
    /// <para>
    /// Stacks are spent by the log rather than by this analyzer: a cast is doubled whenever the buff
    /// carries a stack at the moment it happens, and the buff's own stack events decide when that stops
    /// being true. No log held locally has a player running the talent, so the doubling is modelled from
    /// the Season 3 constants and has not been checked against live data.
    /// </para>
    /// </summary>
    public const double PunishingStrikesScaler = 2.0;

    /// <summary>The stack count <c>BlockToIncreasedPower.StacksAtProc</c> gives the buff when a block procs it.</summary>
    public const int PunishingStrikesStacksAtProc = 2;

    /// <summary>Every source-to-target pair that generated reduction, ordered by the seconds it wasted.</summary>
    public IReadOnlyList<CooldownContribution> Contributions => Result.Contributions;

    /// <summary>Each source ability's totals, ordered by the seconds it wasted.</summary>
    public IReadOnlyList<CooldownContribution> BySource => Result.BySource;

    /// <summary>Reduction generated this pull, in milliseconds.</summary>
    public int GeneratedMs => Result.GeneratedMs;

    /// <summary>Reduction that shortened a running cooldown, in milliseconds.</summary>
    public int AppliedMs => Result.AppliedMs;

    /// <summary>Reduction generated against an ability that was already available, in milliseconds.</summary>
    public int WastedMs => Result.GeneratedMs - Result.AppliedMs;

    /// <summary>Share (0-1) of generated reduction that landed on a running cooldown.</summary>
    public double Efficiency => Result.GeneratedMs > 0 ? (double)Result.AppliedMs / Result.GeneratedMs : 0;

    /// <summary>Whether a Spirit ability was active at any point this pull, doubling the reductions made under it.</summary>
    public bool SawActiveUltimate { get; private set; }

    /// <summary>Whether the player took Punishing Strikes, whose block proc doubles the next reduction.</summary>
    public bool HasPunishingStrikes => Owner.SelectedCombatant.HasTalent(HelenaTalents.PunishingStrikes);

    /// <summary>Combo casts made with a Punishing Strikes stack up, each worth double its table value.</summary>
    public int PunishingStrikesCasts { get; private set; }

    /// <summary>
    /// Casts of a combo target made while the cooldown model believed that ability had no charge left,
    /// keyed by spell id. Every one of these is the model being wrong - the cast happened, so the
    /// ability was available - and each one means the model was holding a cooldown that had really
    /// already recovered, which inflates <see cref="AppliedMs"/> at the expense of <see cref="WastedMs"/>.
    /// Read it as the confidence interval on the seconds above.
    /// </summary>
    public IReadOnlyDictionary<int, int> CastsWhileModelledUnavailable => _castsWhileModelledUnavailable;

    /// <summary>Total casts the cooldown model believed were impossible, across every combo target.</summary>
    public int ModelDisagreements => Result.ModelDisagreements;

    /// <summary>Casts of a combo target this pull, whatever the model believed about them.</summary>
    public int TargetCasts => Result.TargetCasts;

    /// <summary>Share (0-1) of combo-target casts the cooldown model agreed were possible.</summary>
    public double ModelAgreement =>
        Result.TargetCasts > 0 ? 1 - ((double)Result.ModelDisagreements / Result.TargetCasts) : 1;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateApplied(ApplyBuffEvent buffEvent)
    {
        _ultimateActive = true;
        SawActiveUltimate = true;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateRefreshed(RefreshBuffEvent buffEvent)
    {
        _ultimateActive = true;
        SawActiveUltimate = true;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateRemoved(RemoveBuffEvent buffEvent) => _ultimateActive = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesApplied(ApplyBuffEvent buffEvent) =>
        _punishingStrikesStacks = PunishingStrikesStacksAtProc;

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesStacked(ApplyBuffStackEvent buffEvent) =>
        _punishingStrikesStacks = buffEvent.Stack;

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesStackRemoved(RemoveBuffStackEvent buffEvent) =>
        _punishingStrikesStacks = buffEvent.Stack;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesRemoved(RemoveBuffEvent buffEvent) => _punishingStrikesStacks = 0;

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.MeasuredStrike),
        nameof(Spells.PowerStrike),
        nameof(Spells.ShieldSlam),
        nameof(Spells.ShieldThrow),
        nameof(Spells.Shockwave),
        nameof(Spells.HoldTheLine)])]
    private void OnComboSource(CastEvent castEvent)
    {
        _sourceCasts[castEvent.Ability.Id] = _sourceCasts.GetValueOrDefault(castEvent.Ability.Id) + 1;

        var scaler = _ultimateActive ? ActiveUltimateScaler : 1.0;

        if (_punishingStrikesStacks > 0)
        {
            scaler *= PunishingStrikesScaler;
            PunishingStrikesCasts++;
        }

        foreach (var combo in Combos)
        {
            if (combo.SourceSpellId != castEvent.Ability.Id) continue;

            var requested = (int)Math.Round(combo.ReductionMs * scaler);
            var reduction = SpellUsable.ReduceCooldown(combo.TargetSpellId, requested, castEvent.Timestamp);

            var key = (combo.SourceSpellId, combo.TargetSpellId);
            if (!_contributions.TryGetValue(key, out var contribution))
                _contributions[key] = contribution = new Contribution();

            contribution.Generated += reduction.GeneratedMs;
            contribution.Applied += reduction.AppliedMs;
            contribution.Events++;
        }
    }

    /// <summary>
    /// Mirrors the cooldown model's charge count for every combo target. <see cref="SpellUsable"/>
    /// dispatches ahead of the pull tier and its fabricated updates are spliced in after the event
    /// being processed, so this count still holds the pre-cast state when a cast handler here runs -
    /// which is the only point at which it can be compared against a cast actually happening.
    /// </summary>
    [On<UpdateSpellUsableEvent>(Spells = [
        nameof(Spells.ShieldSlam),
        nameof(Spells.ShieldThrow),
        nameof(Spells.Shockwave),
        nameof(Spells.ShieldsUp)])]
    private void OnTargetChargeChanged(UpdateSpellUsableEvent usableEvent) =>
        _modelledCharges[usableEvent.Ability.Id] = usableEvent.ChargesAvailable;

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.ShieldSlam),
        nameof(Spells.ShieldThrow),
        nameof(Spells.Shockwave),
        nameof(Spells.ShieldsUp)])]
    private void OnComboTargetCast(CastEvent castEvent)
    {
        _targetCasts++;

        if (ModelledCharges(castEvent.Ability.Id) > 0) return;

        _castsWhileModelledUnavailable[castEvent.Ability.Id] =
            _castsWhileModelledUnavailable.GetValueOrDefault(castEvent.Ability.Id) + 1;
    }

    private int ModelledCharges(int spellId) =>
        _modelledCharges.TryGetValue(spellId, out var charges)
            ? charges
            : Owner.GetModule<Abilities>()?.GetMaxCharges(spellId) ?? 1;

    private Computed Result => field ??= Compute();

    private Computed Compute()
    {
        var pairs = new List<CooldownContribution>(_contributions.Count);
        var bySource = new Dictionary<int, Contribution>();
        var generated = 0;
        var applied = 0;

        foreach (var ((source, target), contribution) in _contributions)
        {
            pairs.Add(new CooldownContribution(
                source, target, contribution.Events, contribution.Generated, contribution.Applied));

            generated += contribution.Generated;
            applied += contribution.Applied;

            if (!bySource.TryGetValue(source, out var totals))
                bySource[source] = totals = new Contribution();

            totals.Generated += contribution.Generated;
            totals.Applied += contribution.Applied;
        }

        pairs.Sort(static (left, right) => right.WastedMs.CompareTo(left.WastedMs));

        var sources = new List<CooldownContribution>(bySource.Count);
        foreach (var (source, totals) in bySource)
        {
            sources.Add(new CooldownContribution(
                source, null, _sourceCasts.GetValueOrDefault(source), totals.Generated, totals.Applied));
        }

        sources.Sort(static (left, right) => right.WastedMs.CompareTo(left.WastedMs));

        var disagreements = 0;
        foreach (var count in _castsWhileModelledUnavailable.Values) disagreements += count;

        return new Computed(pairs, sources, generated, applied, disagreements, _targetCasts);
    }

    private sealed class Contribution
    {
        public int Generated { get; set; }
        public int Applied { get; set; }
        public int Events { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<CooldownContribution> Contributions,
        IReadOnlyList<CooldownContribution> BySource,
        int GeneratedMs,
        int AppliedMs,
        int ModelDisagreements,
        int TargetCasts);
}

/// <summary>
/// One entry in Veteran of War's cooldown-reduction table.
/// </summary>
/// <param name="SourceSpellId">The ability whose cast generates the reduction.</param>
/// <param name="TargetSpellId">The ability whose cooldown is shortened.</param>
/// <param name="ReductionMs">The reduction one cast generates before any scaler.</param>
public sealed record CooldownCombo(int SourceSpellId, int TargetSpellId, int ReductionMs);

/// <summary>
/// How much reduction one source generated, and how much of it landed.
/// </summary>
/// <param name="SourceSpellId">The ability that generated the reduction.</param>
/// <param name="TargetSpellId">The ability it was aimed at, or <c>null</c> for a source's combined totals.</param>
/// <param name="Events">Casts that generated reduction on this pairing.</param>
/// <param name="GeneratedMs">Reduction generated, after Ability Cooldown Reduction scaling.</param>
/// <param name="AppliedMs">The share of <paramref name="GeneratedMs"/> that shortened a running cooldown.</param>
public sealed record CooldownContribution(
    int SourceSpellId,
    int? TargetSpellId,
    int Events,
    int GeneratedMs,
    int AppliedMs)
{
    /// <summary>Reduction generated while the target was already available.</summary>
    public int WastedMs => GeneratedMs - AppliedMs;

    /// <summary>Share (0-1) of the generated reduction that landed.</summary>
    public double Efficiency => GeneratedMs > 0 ? (double)AppliedMs / GeneratedMs : 0;
}
