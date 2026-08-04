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
    private readonly Dictionary<int, int> _castableSince = [];
    private readonly Dictionary<int, int> _idleSince = [];
    private readonly List<HoldTheLinePress> _holdTheLinePresses = [];

    private bool _ultimateActive;
    private int _punishingStrikesStacks;

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

    /// <summary>
    /// The abilities Hold the Line shortens, taken from <see cref="Combos"/> so the table stays the
    /// only place the pairing is stated.
    /// </summary>
    public static IReadOnlyList<int> HoldTheLineTargets { get; } =
    [
        .. Combos.Where(combo => combo.SourceSpellId == Spells.HoldTheLine.FSLID)
            .Select(combo => combo.TargetSpellId),
    ];

    /// <summary>
    /// Every Hold the Line press, carrying how long each of its targets had already been available.
    /// Recorded before the press hands its reduction out, so an ability shows the state it was
    /// actually pressed on rather than the state the reduction left it in.
    /// </summary>
    public IReadOnlyList<HoldTheLinePress> HoldTheLinePresses => _holdTheLinePresses;

    /// <summary>Every source-to-target pair that generated reduction, ordered by the seconds it wasted.</summary>
    public IReadOnlyList<CooldownContribution> Contributions => Result.Contributions;

    /// <summary>Each source ability's totals, ordered by the seconds it wasted.</summary>
    public IReadOnlyList<CooldownContribution> BySource => Result.BySource;

    /// <summary>Every combo's reduction this pull, and how much of it landed.</summary>
    public CooldownReductionResult CooldownReduction => Result.CooldownReduction;

    /// <summary>Whether a Spirit ability was active at any point this pull, doubling the reductions made under it.</summary>
    public bool SawActiveUltimate { get; private set; }

    /// <summary>Whether the player took Punishing Strikes, whose block proc doubles the next reduction.</summary>
    public bool HasPunishingStrikes => Owner.SelectedCombatant.HasTalent(HelenaTalents.PunishingStrikes);

    /// <summary>Combo casts made with a Punishing Strikes stack up, each worth double its table value.</summary>
    public int PunishingStrikesCasts { get; private set; }

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

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart)
    {
        foreach (var target in HoldTheLineTargets)
        {
            if (SpellUsable.IsAvailable(target)) _castableSince[target] = pullStart.Timestamp;
            if (!SpellUsable.IsOnCooldown(target)) _idleSince[target] = pullStart.Timestamp;
        }
    }

    [On<UpdateSpellUsableEvent>(Spells = [
        nameof(Spells.ShieldSlam),
        nameof(Spells.ShieldThrow),
        nameof(Spells.Shockwave),
        nameof(Spells.ShieldsUp)])]
    private void OnTargetUsabilityChanged(UpdateSpellUsableEvent usableEvent)
    {
        if (usableEvent.IsAvailable) _castableSince.TryAdd(usableEvent.Ability.Id, usableEvent.Timestamp);
        else _castableSince.Remove(usableEvent.Ability.Id);

        if (usableEvent.IsOnCooldown) _idleSince.Remove(usableEvent.Ability.Id);
        else _idleSince.TryAdd(usableEvent.Ability.Id, usableEvent.Timestamp);
    }

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

        if (castEvent.Ability.Id == Spells.HoldTheLine.FSLID) RecordHoldTheLinePress(castEvent.Timestamp);

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

            contribution.CooldownReduction += reduction;
            contribution.Events++;
        }
    }

    private void RecordHoldTheLinePress(int timestamp)
    {
        var targets = new List<HoldTheLineTarget>(HoldTheLineTargets.Count);
        foreach (var target in HoldTheLineTargets)
        {
            var resets = target == Spells.ShieldSlam.FSLID;
            var available = resets ? SpellUsable.IsAvailable(target) : !SpellUsable.IsOnCooldown(target);
            var since = resets ? _castableSince : _idleSince;

            targets.Add(new HoldTheLineTarget(
                target,
                available ? Math.Max(0, timestamp - since.GetValueOrDefault(target, Pull.StartTime)) : null));
        }

        _holdTheLinePresses.Add(new HoldTheLinePress(timestamp, targets));
    }

    private Computed Result => field ??= Compute();

    private Computed Compute()
    {
        var pairs = new List<CooldownContribution>(_contributions.Count);
        var bySource = new Dictionary<int, Contribution>();
        var total = new CooldownReductionResult();

        foreach (var ((source, target), contribution) in _contributions)
        {
            pairs.Add(new CooldownContribution(
                source, target, contribution.Events, contribution.CooldownReduction));

            total += contribution.CooldownReduction;

            if (!bySource.TryGetValue(source, out var totals))
                bySource[source] = totals = new Contribution();

            totals.CooldownReduction += contribution.CooldownReduction;
        }

        pairs.Sort(static (left, right) =>
            right.CooldownReduction.Wasted.CompareTo(left.CooldownReduction.Wasted));

        var sources = new List<CooldownContribution>(bySource.Count);
        foreach (var (source, totals) in bySource)
        {
            sources.Add(new CooldownContribution(
                source, null, _sourceCasts.GetValueOrDefault(source), totals.CooldownReduction));
        }

        sources.Sort(static (left, right) =>
            right.CooldownReduction.Wasted.CompareTo(left.CooldownReduction.Wasted));

        return new Computed(pairs, sources, total);
    }

    private sealed class Contribution
    {
        public CooldownReductionResult CooldownReduction { get; set; }
        public int Events { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<CooldownContribution> Contributions,
        IReadOnlyList<CooldownContribution> BySource,
        CooldownReductionResult CooldownReduction);
}

/// <summary>
/// One Hold the Line press and the state its targets were in when it landed.
/// </summary>
/// <param name="Timestamp">When the press happened.</param>
/// <param name="Targets">Each ability the ten-second row shortens, in table order.</param>
public sealed record HoldTheLinePress(int Timestamp, IReadOnlyList<HoldTheLineTarget> Targets);

/// <summary>
/// One ability's state at a Hold the Line press.
/// </summary>
/// <param name="SpellId">The ability the press shortens.</param>
/// <param name="AvailableForMs">
/// How long it had already been available when the press landed, or <c>null</c> when the press had
/// something to give it back.
/// <para>
/// Available means two different things across the four, because the press is worth two different
/// things to them. Ten seconds covers the whole of Shield Slam's seven-and-a-half second cooldown, so
/// the press resets it and what matters is whether it was castable at all - it carries two charges,
/// and one in hand is a reset with nothing to give back. For the other three the ten seconds is a
/// reduction, wasted only when nothing was recharging for it to come off.
/// </para>
/// </param>
public sealed record HoldTheLineTarget(int SpellId, int? AvailableForMs)
{
    /// <summary>Whether the ability was sitting available, so the press had nothing to shorten on it.</summary>
    public bool WasAvailable => AvailableForMs.HasValue;
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
/// <param name="CooldownReduction">What those casts generated, and how much of it landed.</param>
public sealed record CooldownContribution(
    int SourceSpellId,
    int? TargetSpellId,
    int Events,
    CooldownReductionResult CooldownReduction);
