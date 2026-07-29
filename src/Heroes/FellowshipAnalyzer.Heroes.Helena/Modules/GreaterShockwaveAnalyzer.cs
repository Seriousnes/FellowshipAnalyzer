using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Helena.Statistics;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Measures Greater Shockwave's two Season 3 halves against each other. Each Shockwave returns a tenth
/// of maximum Toughness and amplifies its own damage by up to thirty percent, both sized from how much
/// Toughness was held at the moment of the cast - and the two want opposite things from it.
/// <para>
/// Amplification is read straight off that share, since it scales linearly with it. Wasted generation
/// is what the returned tenth had no room for: a cast made within a tenth of maximum loses the
/// difference, and a cast made at maximum loses all of it.
/// </para>
/// <para>
/// The Season 2 talent of the same name shortened Shockwave's cooldown. Season 3's does not, and the
/// cooldown constant still sitting in the data block belongs to the retired version.
/// </para>
/// </summary>
[RequiresTalent(HelenaTalents.GreaterShockwave)]
public sealed partial class GreaterShockwaveAnalyzer : Analyzer
{
    private double _amplificationTotal;
    private double _wastedShareTotal;

    /// <summary>
    /// <c>Shockwave.Talent.IncreasedToughnessAndDamage.DamageMultiplier.DamageMultiplierAtFullToughness</c>:
    /// the damage a cast made at maximum Toughness adds, scaling linearly down to nothing at none.
    /// </summary>
    public const double AmplificationAtFullToughness = 0.3;

    /// <summary>
    /// <c>Shockwave.Talent.IncreasedToughnessAndDamage.AdditionalToughnessBasedOnMax</c>: the share of
    /// maximum Toughness each cast returns on top of Shockwave's own per-hit generation.
    /// </summary>
    public const double AdditionalToughnessShare = 0.1;

    /// <inheritdoc/>
    public override Type? StatisticsComponentType =>
        MeasuredCasts > 0 ? typeof(GreaterShockwaveStatistics) : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>Shockwave casts that carried a Toughness reading to size both halves from.</summary>
    public int MeasuredCasts { get; private set; }

    /// <summary>Shockwave casts that carried no Toughness reading, and so are absent from both figures.</summary>
    public int UnmeasuredCasts { get; private set; }

    /// <summary>The damage amplification the talent granted per cast, averaged over the fight, as a fraction.</summary>
    public double AverageAmplification => MeasuredCasts > 0 ? _amplificationTotal / MeasuredCasts : 0;

    /// <summary>Share (0-1) of the amplification ceiling those casts reached.</summary>
    public double AmplificationShare => AverageAmplification / AmplificationAtFullToughness;

    /// <summary>Share (0-1) of the Toughness the talent returned that there was no room to hold.</summary>
    public double ToughnessWastedShare => MeasuredCasts > 0 ? _wastedShareTotal / MeasuredCasts : 0;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Shockwave))]
    private void OnShockwave(CastEvent castEvent)
    {
        if (FindToughness(castEvent.SourceResources) is not { Max: > 0 } toughness)
        {
            UnmeasuredCasts++;
            return;
        }

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
