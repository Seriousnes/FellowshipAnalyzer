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
