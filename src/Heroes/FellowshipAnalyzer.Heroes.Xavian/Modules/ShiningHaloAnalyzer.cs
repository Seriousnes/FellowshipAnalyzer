using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class ShiningHaloAnalyzer : Analyzer
{
    public int Casts { get; private set; }

    public int BrilliantFlashCasts { get; private set; }

    public int BrilliantFlashInHalo { get; private set; }

    public int ActiveMs =>
        AuraWindowLedger.ActiveMs([.. Owner.SelectedCombatant.GetAuraWindows(Spells.ShiningHaloSelfBuff, Pull.StartTime, Pull.EndTime)]);

    public double Uptime
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)ActiveMs / duration, 0, 1) : 0;
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShiningHalo))]
    private void OnCast() => Casts++;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.BrilliantFlash))]
    private void OnBrilliantFlash(CastEvent castEvent)
    {
        BrilliantFlashCasts++;
        if (Owner.SelectedCombatant.HasBuff(Spells.ShiningHaloSelfBuff, castEvent.Timestamp))
            BrilliantFlashInHalo++;
    }
}
