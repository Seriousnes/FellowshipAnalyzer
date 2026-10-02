using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;
using FellowshipAnalyzer.Core.Events;

using XavianTalents = FellowshipAnalyzer.Core.Common.Spells.XavianTalents;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

public interface ISolarShieldAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SolarShieldAnalyzer : MajorDefensiveAnalyzer, ISolarShieldAnalyzer
{
    protected override int DefensiveSpellId => Spells.SolarShield.FSLID;

    public int Casts { get; private set; }

    public int CastsOnSelf { get; private set; }

    public double CastsPerMinute
    {
        get
        {
            var minutes = (Pull.EndTime - Pull.StartTime) / 60_000d;
            return minutes > 0 ? Casts / minutes : 0;
        }
    }

    public bool MagicWardTaken => Owner.SelectedCombatant.HasTalent(XavianTalents.MagicWard);

    public int MagicWardActiveMs =>
        AuraWindowLedger.ActiveMs([.. Owner.SelectedCombatant.GetAuraWindows(Spells.MagicWard, Pull.StartTime, Pull.EndTime)]);

    public double MagicWardUptime
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)MagicWardActiveMs / duration, 0, 1) : 0;
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SolarShield))]
    private void OnCast(CastEvent castEvent)
    {
        Casts++;
        if (castEvent.TargetId == PlayerId) CastsOnSelf++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SolarShieldAbsorb))]
    private void OnApplied(ApplyBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SolarShieldAbsorb))]
    private void OnRefreshed(RefreshBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SolarShieldAbsorb))]
    private void OnRemoved(RemoveBuffEvent buffEvent) => CloseWindow(buffEvent.Timestamp);

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent) => RecordDamageTaken(damageEvent);
}
