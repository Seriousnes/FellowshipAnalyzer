using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Multishot casts and how many of them were "regular" while an Empowered Multishot
/// charge was available. Empowered Multishot should always be consumed before regular casts.
/// </summary>
/// <remarks>
/// Empowered Multishot is detected by buff name <c>Empowered Multishot</c> applied to the
/// player. When a Multishot cast occurs while that buff is active, it counts as a consumption;
/// when Multishot is cast without it, but a prior empowered availability window existed, it
/// is recorded as a waste.
/// </remarks>
public sealed partial class EmpoweredMultishotWasteAnalyzer : Analyzer
{
    private const string EmpoweredMultishotName = "Empowered Multishot";

    private bool _empoweredAvailable;
    private int? _empoweredAvailableSinceTimestamp;

    private int _regularCasts;
    private int _empoweredConsumed;
    private int _wastedExpirations;

    public int RegularCasts => _regularCasts;
    public int EmpoweredConsumed => _empoweredConsumed;
    public int WastedExpirations => _wastedExpirations;

    public override Type? StatisticsComponentType => typeof(EmpoweredMultishotWasteStatistics);

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApplyBuff(ApplyBuffEvent e)
    {
        if (!IsEmpoweredMultishot(e.Ability)) return;

        _empoweredAvailable = true;
        _empoweredAvailableSinceTimestamp = e.Timestamp;
    }

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoveBuff(RemoveBuffEvent e)
    {
        if (!IsEmpoweredMultishot(e.Ability)) return;

        if (_empoweredAvailable)
        {
            _wastedExpirations++;
        }

        _empoweredAvailable = false;
        _empoweredAvailableSinceTimestamp = null;
    }

    [On<CastEvent>(By = Actor.Player, Spell = SpellIds.Multishot)]
    private void OnMultishot(CastEvent e)
    {
        if (_empoweredAvailable)
        {
            _empoweredConsumed++;
            _empoweredAvailable = false;
            _empoweredAvailableSinceTimestamp = null;
        }
        else
        {
            _regularCasts++;
        }
    }

    private static bool IsEmpoweredMultishot(Ability ability) =>
        string.Equals(ability.Name, EmpoweredMultishotName, StringComparison.Ordinal);
}
