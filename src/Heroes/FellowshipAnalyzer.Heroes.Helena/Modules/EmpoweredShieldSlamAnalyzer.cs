using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>Keeps the Shield Slam barrier on its own read surface, apart from any other absorb.</summary>
public interface IEmpoweredShieldSlamAnalyzer : IAnalyzerSurface;

/// <summary>
/// Measures what Hold the Line's empowerment turned into. Hold the Line empowers the next Shield
/// Slam, and that Shield Slam lays a barrier; the barrier's own removal carries whatever absorb it
/// still held, so a barrier that expired with absorb left is measurable waste rather than an
/// inference.
/// <para>
/// The empowerment's source is settled from the log, not from the spell data: the buff lands within a
/// millisecond of every Hold the Line cast in the validation report, while the movement buff the
/// Season 3 dump attributes to Hold the Line never appears at all.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class EmpoweredShieldSlamAnalyzer : AbsorbAnalyzer, IEmpoweredShieldSlamAnalyzer
{
    private int _empowermentsGranted;
    private int _empowermentsExpired;
    private bool _empowermentOpen;
    private bool _empowermentConsumed;

    /// <summary>Hold the Line casts that empowered a Shield Slam this pull.</summary>
    public int EmpowermentsGranted => _empowermentsGranted;

    /// <summary>
    /// Empowerments that fell off with no Shield Slam cast under them. Zero on every pull of the
    /// validation report, because the buff is removed the instant a Shield Slam spends it and Shield
    /// Slam comes round faster than the buff can lapse - so treat a non-zero reading as the signal and
    /// zero as the expected case, never as a score.
    /// </summary>
    public int EmpowermentsExpired => _empowermentsExpired;

    /// <summary>
    /// Barrier applications, counting a refresh onto a live barrier alongside a fresh one. Every
    /// application in the validation report landed inside an empowerment window with a Shield Slam
    /// within half a second, and no application fell outside one, so the barrier has no second source.
    /// </summary>
    public int BarrierApplications => Applications;

    /// <summary>
    /// Barrier windows this pull. A barrier laid while one is already up refreshes it rather than
    /// stacking, so consecutive empowered Shield Slams inside one duration share a window and
    /// <see cref="BarrierApplications"/> runs ahead of this.
    /// </summary>
    public int BarrierWindows => ShieldsLaid;

    /// <summary>Every barrier this pull, in encounter order.</summary>
    public IReadOnlyList<AbsorbUse> Barriers => Shields;

    /// <summary>Barriers that expired still holding absorb.</summary>
    public int BarriersExpiredUnspent => ShieldsExpiredUnspent;

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
    private void OnBarrierApplied(ApplyBuffEvent buffEvent) => OpenShield(buffEvent);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierRefreshed(RefreshBuffEvent buffEvent) => OpenShield(buffEvent);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnBarrierRemoved(RemoveBuffEvent buffEvent) => CloseShield(buffEvent);

    [On<AbsorbedEvent>(To = Actor.Player, Spell = nameof(Spells.ShieldSlamAbsorb))]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent) => RecordAbsorbed(absorbedEvent);
}
