using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public interface IEmpoweredShieldSlamAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class EmpoweredShieldSlamAnalyzer : AbsorbAnalyzer, IEmpoweredShieldSlamAnalyzer
{
    private int _empowermentsGranted;
    private int _empowermentsExpired;
    private bool _empowermentOpen;
    private bool _empowermentConsumed;

    public int EmpowermentsGranted => _empowermentsGranted;

    public int EmpowermentsExpired => _empowermentsExpired;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorbBuffSelfBuff))]
    private void OnEmpowered(ApplyBuffEvent buffEvent)
    {
        _empowermentsGranted++;
        _empowermentOpen = true;
        _empowermentConsumed = false;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlam))]
    private void OnShieldSlamCast(CastEvent castEvent)
    {
        if (_empowermentOpen) _empowermentConsumed = true;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorbBuffSelfBuff))]
    private void OnEmpowermentRemoved(RemoveBuffEvent buffEvent)
    {
        if (_empowermentOpen && !_empowermentConsumed) _empowermentsExpired++;

        _empowermentOpen = false;
        _empowermentConsumed = false;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierApplied(ApplyBuffEvent buffEvent) => OpenShield(buffEvent, buffEvent.Absorb ?? 0);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierRefreshed(RefreshBuffEvent buffEvent) => OpenShield(buffEvent, buffEvent.Absorb ?? 0);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierRemoved(RemoveBuffEvent buffEvent) => CloseShield(buffEvent);

    [On<AbsorbedEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent) => RecordAbsorbed(absorbedEvent);
}
