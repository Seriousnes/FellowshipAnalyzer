using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public interface IShieldsUpAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class ShieldsUpAnalyzer : MajorDefensiveAnalyzer, IShieldsUpAnalyzer
{
    private readonly List<int> _castTimestamps = [];

    private int _chargeCappedMs;
    private int? _cappedSince;
    private int _maxCharges;

    protected override int DefensiveSpellId => Spells.ShieldsUp.FSLID;

    public int Casts => _castTimestamps.Count;

    public int ChargeCappedMs => Result.ChargeCappedMs;

    public double ChargeCappedShare
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)ChargeCappedMs / duration, 0, 1) : 0;
        }
    }

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
