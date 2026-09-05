using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

[RequiresTalent(HelenaTalents.GreaterShockwave)]
public sealed partial class GreaterShockwaveAnalyzer : Analyzer
{
    private double _amplificationTotal;
    private double _wastedShareTotal;

    public const double AmplificationAtFullToughness = 0.3;

    public const double AdditionalToughnessShare = 0.1;

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    public int MeasuredCasts { get; private set; }

    public double AverageAmplification => MeasuredCasts > 0 ? _amplificationTotal / MeasuredCasts : 0;

    public double AmplificationShare => AverageAmplification / AmplificationAtFullToughness;

    public double ToughnessWastedShare => MeasuredCasts > 0 ? _wastedShareTotal / MeasuredCasts : 0;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Shockwave))]
    private void OnShockwave(CastEvent castEvent)
    {
        if (FindToughness(castEvent.SourceResources) is not { Max: > 0 } toughness) return;

        MeasuredCasts++;

        var share = Math.Clamp(toughness.Amount / (double)toughness.Max, 0, 1);
        _amplificationTotal += share * AmplificationAtFullToughness;
        _wastedShareTotal += Math.Clamp((AdditionalToughnessShare - (1 - share)) / AdditionalToughnessShare, 0, 1);
    }

    private static ClassResource? FindToughness(ActorResources? resources)
    {
        if (resources?.Resources is not { Count: > 0 } list) return null;

        foreach (var resource in list)
            if (resource.Type == ResourceTypes.Secondary)
                return resource;

        return null;
    }
}
