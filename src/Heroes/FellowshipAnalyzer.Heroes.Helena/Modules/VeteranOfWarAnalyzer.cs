using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

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

    public const double ActiveUltimateScaler = 2.0;

    public const double PunishingStrikesScaler = 2.0;

    public const int PunishingStrikesStacksAtProc = 2;

    public static IReadOnlyList<int> HoldTheLineTargets { get; } =
    [
        .. Combos.Where(combo => combo.SourceSpellId == Spells.HoldTheLine.FSLID)
            .Select(combo => combo.TargetSpellId),
    ];

    public static IReadOnlyList<int> ReductionTargets { get; } =
        [.. Combos.Select(combo => combo.TargetSpellId).Distinct()];

    public IReadOnlyList<HoldTheLinePress> HoldTheLinePresses => _holdTheLinePresses;

    public IReadOnlyList<CooldownContribution> Contributions => Result.Contributions;

    public IReadOnlyList<CooldownContribution> BySource => Result.BySource;

    public CooldownReductionResult CooldownReduction => Result.CooldownReduction;

    public bool SawActiveUltimate { get; private set; }

    public bool HasPunishingStrikes => Owner.SelectedCombatant.HasTalent(HelenaTalents.PunishingStrikes);

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

        var availability = castEvent.Ability.Id == Spells.HoldTheLine.FSLID
            ? CaptureHoldTheLineAvailability(castEvent.Timestamp)
            : null;

        var scaler = _ultimateActive ? ActiveUltimateScaler : 1.0;

        if (_punishingStrikesStacks > 0)
        {
            scaler *= PunishingStrikesScaler;
            PunishingStrikesCasts++;
        }

        var pressReductions = availability is null ? null : new Dictionary<int, CooldownReductionResult>();

        foreach (var combo in Combos)
        {
            if (combo.SourceSpellId != castEvent.Ability.Id) continue;

            var requested = (int)Math.Round(combo.ReductionMs * scaler);
            var reduction = SpellUsable.ReduceCooldown(combo.TargetSpellId, requested, castEvent.Timestamp);

            pressReductions?.Add(combo.TargetSpellId, reduction);

            var key = (combo.SourceSpellId, combo.TargetSpellId);
            if (!_contributions.TryGetValue(key, out var contribution))
                _contributions[key] = contribution = new Contribution();

            contribution.CooldownReduction += reduction;
            contribution.Events++;
        }

        if (availability is null) return;

        _holdTheLinePresses.Add(new HoldTheLinePress(
            castEvent.Timestamp,
            [.. availability.Select(entry => new HoldTheLineTarget(
                entry.SpellId,
                entry.AvailableForMs,
                pressReductions!.GetValueOrDefault(entry.SpellId)))]));
    }

    private List<(int SpellId, int? AvailableForMs)> CaptureHoldTheLineAvailability(int timestamp)
    {
        var targets = new List<(int SpellId, int? AvailableForMs)>(HoldTheLineTargets.Count);
        foreach (var target in HoldTheLineTargets)
        {
            var resets = target == Spells.ShieldSlam.FSLID;
            var available = resets ? SpellUsable.IsAvailable(target) : !SpellUsable.IsOnCooldown(target);
            var since = resets ? _castableSince : _idleSince;

            targets.Add((
                target,
                available ? Math.Max(0, timestamp - since.GetValueOrDefault(target, Pull.StartTime)) : null));
        }

        return targets;
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

public sealed record HoldTheLinePress(int Timestamp, IReadOnlyList<HoldTheLineTarget> Targets);

public sealed record HoldTheLineTarget(
    int SpellId,
    int? AvailableForMs,
    CooldownReductionResult CooldownReduction)
{
    public bool WasAvailable => AvailableForMs.HasValue;
}

public sealed record CooldownCombo(int SourceSpellId, int TargetSpellId, int ReductionMs);

public sealed record CooldownContribution(
    int SourceSpellId,
    int? TargetSpellId,
    int Events,
    CooldownReductionResult CooldownReduction);
