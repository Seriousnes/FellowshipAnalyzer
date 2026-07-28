using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// The read surface Shields Up analysis is published under. Helena runs more than one
/// <see cref="MajorDefensiveAnalyzer"/> on the same pull, and the shared base would otherwise be the
/// surface both are indexed against, so each defensive names its own.
/// </summary>
public interface IShieldsUpAnalyzer : IAnalyzerSurface;

/// <summary>
/// Measures Shields Up: the damage its windows covered, and the charge economy behind them. Two
/// charges recharging on a shared cooldown means a charge sitting full is recharge time that never
/// happened, so time at full charges is counted as its own absolute waste figure independent of how
/// the windows themselves went.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class ShieldsUpAnalyzer : MajorDefensiveAnalyzer, IShieldsUpAnalyzer
{
    private readonly List<int> _castTimestamps = [];

    private int _chargeCappedMs;
    private int? _cappedSince;
    private int _maxCharges;

    /// <inheritdoc/>
    protected override int DefensiveSpellId => Spells.ShieldsUp.FSLID;

    /// <summary>Shields Up casts this pull.</summary>
    public int Casts => _castTimestamps.Count;

    /// <summary>Milliseconds spent holding every Shields Up charge, with none recharging.</summary>
    public int ChargeCappedMs => Result.ChargeCappedMs;

    /// <summary>Share (0-1) of the pull spent holding every charge.</summary>
    public double ChargeCappedShare
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)ChargeCappedMs / duration, 0, 1) : 0;
        }
    }

    /// <summary>The charge count Shields Up carries, as the spellbook and the cooldown stream report it.</summary>
    public int MaxCharges => _maxCharges > 0 ? _maxCharges : SpellUsable.ChargesAvailable(Spells.ShieldsUp.FSLID);

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent pullStart)
    {
        _maxCharges = Owner.GetModule<Abilities>()?.GetMaxCharges(Spells.ShieldsUp.FSLID) ?? 0;

        if (_maxCharges > 0 && SpellUsable.ChargesAvailable(Spells.ShieldsUp.FSLID) >= _maxCharges)
            _cappedSince = pullStart.Timestamp;
    }

    [On<UpdateSpellUsableEvent>(Spell = nameof(Spells.ShieldsUp))]
    private void OnChargeStateChanged(UpdateSpellUsableEvent usableEvent)
    {
        if (usableEvent.MaxCharges > 0) _maxCharges = usableEvent.MaxCharges;

        var capped = usableEvent.ChargesAvailable >= usableEvent.MaxCharges;
        if (capped)
        {
            _cappedSince ??= usableEvent.Timestamp;
            return;
        }

        if (_cappedSince is not { } since) return;

        _chargeCappedMs += Math.Max(0, usableEvent.Timestamp - since);
        _cappedSince = null;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldsUp))]
    private void OnShieldsUpCast(CastEvent castEvent) => _castTimestamps.Add(castEvent.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldsUpBuff))]
    private void OnShieldsUpApplied(ApplyBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldsUpBuff))]
    private void OnShieldsUpRefreshed(RefreshBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldsUpBuff))]
    private void OnShieldsUpRemoved(RemoveBuffEvent buffEvent) => CloseWindow(buffEvent.Timestamp);

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent) => RecordDamageTaken(damageEvent);

    private ChargeTotals Result => field ??= ComputeCharges();

    private ChargeTotals ComputeCharges()
    {
        var capped = _chargeCappedMs;
        if (_cappedSince is { } since)
            capped += Math.Max(0, Pull.EndTime - since);

        return new ChargeTotals(capped);
    }

    private sealed record ChargeTotals(int ChargeCappedMs);
}
