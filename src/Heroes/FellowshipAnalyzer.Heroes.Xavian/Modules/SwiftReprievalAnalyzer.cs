using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SwiftReprievalAnalyzer : Analyzer
{
    public const int MaxStacks = 3;

    private readonly List<int> _solarBladesAtCap = [];

    public int SolarBladesCasts { get; private set; }

    public IReadOnlyList<int> SolarBladesAtCapTimestamps => _solarBladesAtCap;

    public int SolarBladesAtCap => _solarBladesAtCap.Count;

    public int BrilliantFlashCasts { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SolarBlades))]
    private void OnSolarBlades(CastEvent castEvent)
    {
        SolarBladesCasts++;
        if (Owner.SelectedCombatant.GetBuffStacks(Spells.SwiftReprieval, castEvent.Timestamp) >= MaxStacks)
            _solarBladesAtCap.Add(castEvent.Timestamp);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.BrilliantFlash))]
    private void OnBrilliantFlash() => BrilliantFlashCasts++;
}
