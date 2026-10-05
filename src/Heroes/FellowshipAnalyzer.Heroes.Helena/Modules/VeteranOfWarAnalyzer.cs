using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Reports, for one pull, what the Veteran of War casts made during it generated and wasted, reading each cast's
/// outcome from <see cref="VeteranOfWar"/>, which applies the reductions for the whole dungeon.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
[Dependency<VeteranOfWar>]
public sealed partial class VeteranOfWarAnalyzer : Analyzer
{
    private readonly Dictionary<(int Source, int Target), Contribution> _contributions = [];
    private readonly Dictionary<int, int> _sourceCasts = [];
    private readonly Dictionary<int, int> _castableSince = [];
    private readonly Dictionary<int, int> _idleSince = [];
    private readonly List<HoldTheLineCast> _holdTheLineCasts = [];

    public List<HoldTheLineCast> HoldTheLineCasts => _holdTheLineCasts;

    public List<CooldownContribution> Contributions => Result.Contributions;

    public List<CooldownContribution> BySource => Result.BySource;

    public CooldownReductionResult CooldownReduction => Result.CooldownReduction;

    public bool UltimateWasActive { get; private set; }

    public bool HasPunishingStrikes => Owner.SelectedCombatant.HasTalent(HelenaTalents.PunishingStrikes);

    public int PunishingStrikesCasts { get; private set; }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateApplied() => UltimateWasActive = true;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateRefreshed() => UltimateWasActive = true;

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart)
    {
        UltimateWasActive = VeteranOfWar.UltimateActive;

        foreach (var target in VeteranOfWar.HoldTheLineTargets)
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
        if (VeteranOfWar.LastCast is not { } cast || !ReferenceEquals(cast.Cast, castEvent)) return;

        _sourceCasts[castEvent.Ability.Id] = _sourceCasts.GetValueOrDefault(castEvent.Ability.Id) + 1;

        if (VeteranOfWar.UltimateActive) UltimateWasActive = true;
        if (cast.UnderPunishingStrikes) PunishingStrikesCasts++;

        foreach (var (target, reduction) in cast.Reductions)
        {
            var key = ((int)castEvent.Ability.Id, target);
            if (!_contributions.TryGetValue(key, out var contribution))
                _contributions[key] = contribution = new Contribution();

            contribution.CooldownReduction += reduction;
            contribution.Events++;
        }

        if (cast.TargetsAvailable is not { } targetsAvailable) return;

        _holdTheLineCasts.Add(new HoldTheLineCast(
            castEvent.Timestamp,
            [.. VeteranOfWar.HoldTheLineTargets.Select(target => new HoldTheLineTarget(
                target,
                AvailableFor(target, targetsAvailable[target], castEvent.Timestamp),
                cast.Reductions.GetValueOrDefault(target)))]));
    }

    private int? AvailableFor(int target, bool available, int timestamp)
    {
        if (!available) return null;

        var since = target == Spells.ShieldSlam.FSLID ? _castableSince : _idleSince;
        return Math.Max(0, timestamp - since.GetValueOrDefault(target, Pull.StartTime));
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
        List<CooldownContribution> Contributions,
        List<CooldownContribution> BySource,
        CooldownReductionResult CooldownReduction);
}

public sealed record HoldTheLineCast(int Timestamp, List<HoldTheLineTarget> Targets);

public sealed record HoldTheLineTarget(
    int SpellId,
    int? AvailableForMs,
    CooldownReductionResult CooldownReduction)
{
    public bool WasAvailable => AvailableForMs.HasValue;
}

public sealed record CooldownContribution(
    int SourceSpellId,
    int? TargetSpellId,
    int Events,
    CooldownReductionResult CooldownReduction);
